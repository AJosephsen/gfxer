using System.Text;
using StrategyGame.Core.Catalog;
using StrategyGame.Core.Models;
using StrategyGame.Core.Models.Board;
using StrategyGame.Core.Models.Cards;
using StrategyGame.Core.Models.Game;

namespace StrategyGame.Core.Services;

/// <summary>
/// All game logic: creating games, playing cards, investing, ending rounds.
/// Loads and saves via GameRepository; resolves card rules via CardCatalog.
/// </summary>
public sealed class GameService(IGameRepository repo, CardCatalog catalog)
{
    // ── Game lifecycle ──────────────────────────────────────────────────────

    public GameState StartGame(string playerName)
    {
        var game = new GameState
        {
            PlayerName = playerName,
            Resources = new ResourceAmount(Food: 8, People: 5, Flux: ResourceAmount.MaxFlux)
        };

        var usableLandIds = catalog.LandCards
            .Where(d => d.Terrain != TerrainType.Wasteland)
            .Select(d => d.Id).ToArray();

        // Starting hand: 3 random useful land cards + 1 Settlement (no wastelands in opening hand)
        for (int i = 0; i < 3; i++)
        {
            var landId = usableLandIds[Random.Shared.Next(usableLandIds.Length)];
            var landDef = catalog.Get(landId);
            game.Hand.Add(LandCard.Create(landId, landDef.Level));
        }

        var settlementDef = catalog.Get("building_settlement");
        game.Hand.Add(BuildingCard.Create("building_settlement", settlementDef.Level));

        // Pre-shuffle 500-card land deck (15 cards burned per round → ~33 rounds max)
        // 30% of cards are wastelands (useless filler)
        var allLandIds = catalog.LandCards.Select(d => d.Id).ToArray();
        game.LandDeck = Enumerable.Range(0, ResourceAmount.LandDeckSize)
            .Select(_ => Random.Shared.Next(100) < 30
                ? "land_wasteland"
                : usableLandIds[Random.Shared.Next(usableLandIds.Length)])
            .ToList();

        repo.Save(game);
        return game;
    }

    public GameState LoadGame(string gameId) => repo.Load(gameId);

    public List<GameSummary> ListGames() => repo.ListAll();

    public void DeleteGame(string gameId) => repo.Delete(gameId);

    // ── Playing cards ───────────────────────────────────────────────────────

    /// <summary>
    /// Play a card from hand onto the next valid board slot.
    /// Land cards go on the next unlocked empty cell; buildings attach to the next compatible land cell without a building.
    /// </summary>
    public (GameState game, string message) PlayCard(string gameId, string cardInstanceId)
    {
        return PlayCardInternal(
            gameId,
            cardInstanceId,
            (game, card, def) => card switch
            {
                LandCard => FindNextLandPlacementCell(game),
                BuildingCard => FindNextBuildingPlacementCell(game, (BuildingDefinition)def),
                _ => throw new InvalidOperationException("Unknown card type.")
            },
            cell => $"slot {GetSlotNumber(cell)}");
    }

    /// <summary>
    /// Play a card from hand onto a specific board cell.
    /// Land cards go on empty cells; buildings go on cells that already have land.
    /// </summary>
    public (GameState game, string message) PlayCard(string gameId, string cardInstanceId, int row, int col)
    {
        return PlayCardInternal(
            gameId,
            cardInstanceId,
            (game, _, _) =>
            {
                ValidateBoardPosition(row, col);
                return game.Board.GetCell(row, col);
            },
            cell => $"cell ({cell.Row},{cell.Col})");
    }

    private (GameState game, string message) PlayCardInternal(
        string gameId,
        string cardInstanceId,
        Func<GameState, CardBase, CardDefinition, BoardCell> resolveCell,
        Func<BoardCell, string> describeCell)
    {
        var game = repo.Load(gameId);

        var idx = game.Hand.FindIndex(c => c.InstanceId == cardInstanceId);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Card '{cardInstanceId}' not found in hand. Use get_hand to list available cards.");

        var card = game.Hand[idx];
        var def = catalog.Get(card.DefinitionId);
        var cell = resolveCell(game, card, def);

        if (cell.IsLocked)
            throw new InvalidOperationException(
                $"{describeCell(cell)} is locked. Place a land card on an adjacent cell first to unlock it.");

        int fluxAmount = card is LandCard lc0
            ? lc0.ComputeFluxCost(def.FluxCost)
            : def.FluxCost;
        var fluxCost = new ResourceAmount(Flux: fluxAmount);
        if (!game.Resources.CanAfford(fluxCost))
            throw new InvalidOperationException(
                $"Not enough Flux to play {def.Name}. Need: {fluxAmount} Flux, Have: {game.Resources.Flux} Flux.");

        string message;

        if (card is LandCard landCard)
        {
            if (cell.Land != null)
                throw new InvalidOperationException(
                    $"{describeCell(cell)} already has {catalog.Get(cell.Land.DefinitionId).Name} land. Choose an empty cell.");

            game.Resources = game.Resources.Subtract(fluxCost);
            cell.Land = landCard;
            game.Board.UnlockAdjacent(cell.Row, cell.Col);
            game.Hand.RemoveAt(idx);
            message = $"Placed {def.Name} land on {describeCell(cell)}. Spent: {fluxAmount} Flux.";
        }
        else if (card is BuildingCard building)
        {
            if (cell.Land == null)
                throw new InvalidOperationException(
                    $"{describeCell(cell)} has no land. Play a land card there first.");

            var bDef = (BuildingDefinition)def;
            var landDef = (LandDefinition)catalog.Get(cell.Land.DefinitionId);
            var isUpgrade = bDef.PlacementRequirements.TargetCard is not null;

            if (!isUpgrade && cell.Building != null)
                throw new InvalidOperationException(
                    $"{describeCell(cell)} already has a {catalog.Get(cell.Building.DefinitionId).Name}.");

            if (!bDef.CanBuildOn(landDef.Terrain))
                throw new InvalidOperationException(
                    $"{bDef.Name} cannot be built on {landDef.Terrain}. " +
                    $"Allowed terrain: {(bDef.AllowedTerrains.Length > 0 ? string.Join(", ", bDef.AllowedTerrains) : "any")}.");

            ValidatePlacementRequirements(game, cell, bDef);

            var totalCost = bDef.PlayCost.Add(fluxCost);
            if (!game.Resources.CanAfford(totalCost))
                throw new InvalidOperationException(
                    $"Cannot afford {bDef.Name}. Need: {totalCost}, Have: {game.Resources}.");

            game.Resources = game.Resources.Subtract(totalCost);

            if (isUpgrade)
            {
                var previous = cell.Building!;
                cell.Building = new BuildingCard
                {
                    InstanceId = previous.InstanceId,
                    DefinitionId = building.DefinitionId,
                    Level = Math.Max(building.Level, previous.Level + 1),
                    IsActive = previous.IsActive,
                    AssignedWorkers = previous.AssignedWorkers,
                    PopulationCapacity = Math.Max(previous.PopulationCapacity, building.PopulationCapacity)
                };
            }
            else
            {
                cell.Building = building;
            }

            game.Hand.RemoveAt(idx);
            message = isUpgrade
                ? $"Upgraded {describeCell(cell)} to {bDef.Name}. Spent: {totalCost}."
                : $"Built {bDef.Name} on {describeCell(cell)} over {landDef.Name}. Spent: {totalCost}.";
            if (bDef.Occupies > 0)
                message += $" Needs {bDef.Occupies} worker{(bDef.Occupies != 1 ? "s" : "")} (assigned at end of round).";
        }
        else
        {
            throw new InvalidOperationException("Unknown card type.");
        }

        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);
        return (game, message);
    }

    // ── Deck draw ───────────────────────────────────────────────────────────

    /// <summary>
    /// Draw a random land card from the infinite map deck.
    /// Costs DrawCardFluxCost Flux.
    /// </summary>
    public (GameState game, string message) DrawFromDeck(string gameId)
    {
        var game = repo.Load(gameId);

        if (game.Hand.Count >= ResourceAmount.MaxHandSize)
            throw new InvalidOperationException(
                $"Hand is full ({ResourceAmount.MaxHandSize}/{ResourceAmount.MaxHandSize}). Discard a card first using discard_card.");

        if (game.Resources.Flux < ResourceAmount.DrawCardFluxCost)
            throw new InvalidOperationException(
                $"Not enough Flux to draw a card. Need: {ResourceAmount.DrawCardFluxCost} Flux, Have: {game.Resources.Flux} Flux.");

        if (game.LandDeck.Count == 0)
            throw new InvalidOperationException(
                "The land deck is exhausted. No more land cards can be drawn.");

        var drawnId = game.LandDeck[0];
        game.LandDeck.RemoveAt(0);
        var drawn = catalog.Get(drawnId);

        var newCard = LandCard.Create(drawnId);
        game.Hand.Add(newCard);
        game.Resources = game.Resources.Subtract(new ResourceAmount(Flux: ResourceAmount.DrawCardFluxCost));

        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);

        var message = $"Drew {drawn.Name} from the map deck (id: {newCard.InstanceId}). " +
                      $"Fertility ×{newCard.Fertility / 10.0:0.0}, Play cost ×{newCard.AccessibilityCost / 10.0:0.0}. " +
                      $"Spent: {ResourceAmount.DrawCardFluxCost} Flux.";
        return (game, message);
    }

    // ── Investment / market ─────────────────────────────────────────────────

    public (GameState game, string message) Invest(string gameId, string cardDefinitionId)
    {
        var game = repo.Load(gameId);
        var def = catalog.Get(cardDefinitionId);

        if (!def.EnabledInGame)
            throw new InvalidOperationException(
                $"{def.Name} is a prototype/example definition and cannot be invested in during live play.");

        if (game.Hand.Count >= ResourceAmount.MaxHandSize)
            throw new InvalidOperationException(
                $"Hand is full ({ResourceAmount.MaxHandSize}/{ResourceAmount.MaxHandSize}). Discard a card first using discard_card.");

        if (!game.Resources.CanAfford(def.InvestCost))
            throw new InvalidOperationException(
                $"Cannot afford {def.Name}. Need: {def.InvestCost}, Have: {game.Resources}.");

        game.Resources = game.Resources.Subtract(def.InvestCost);

        CardBase newCard = def is LandDefinition
            ? LandCard.Create(cardDefinitionId, def.Level)
            : BuildingCard.Create(cardDefinitionId, def.Level);

        game.Hand.Add(newCard);
        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);

        var message = $"Invested in {def.Name}. Paid: {def.InvestCost}. Card added to hand (ID: {newCard.InstanceId}).";
        return (game, message);
    }

    // ── Discard ─────────────────────────────────────────────────────────────

    /// <summary>Move a card from hand to the discard pile.</summary>
    public (GameState game, string message) DiscardCard(string gameId, string cardInstanceId)
    {
        var game = repo.Load(gameId);

        var idx = game.Hand.FindIndex(c => c.InstanceId == cardInstanceId);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Card '{cardInstanceId}' not found in hand. Use get_hand to list available cards.");

        var card = game.Hand[idx];
        var def = catalog.Get(card.DefinitionId);
        game.Hand.RemoveAt(idx);
        game.DiscardPile.Add(card);

        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);
        return (game, $"Discarded {def.Name} (id: {card.InstanceId}).");
    }

    // ── Round processing ────────────────────────────────────────────────────

    public (GameState game, string summary) EndRound(string gameId)
    {
        var game = repo.Load(gameId);
        var sb = new StringBuilder();
        sb.AppendLine($"=== End of Round {game.Round} ===");

        // Collect all active buildings with their cells (row-major order)
        var activeBuildings = game.Board.AllCells()
            .Where(c => c.Building is { IsActive: true })
            .Select(c => ((BuildingDefinition)catalog.Get(c.Building!.DefinitionId), c.Building!, c))
            .ToList();

        // ── Worker distribution (round-robin) ───────────────────────────
        // Reset all assignments
        foreach (var (_, building, _) in activeBuildings)
            building.AssignedWorkers = 0;

        // Distribute workers one at a time, cycling through buildings that need them
        var needsWorkers = activeBuildings
            .Where(x => x.Item1.Occupies > 0)
            .ToList();

        int remaining = game.Resources.People;
        while (remaining > 0 && needsWorkers.Count > 0)
        {
            var toRemove = new List<int>();
            for (int i = 0; i < needsWorkers.Count && remaining > 0; i++)
            {
                var (bDef, building, _) = needsWorkers[i];
                building.AssignedWorkers++;
                remaining--;
                if (building.AssignedWorkers >= bDef.Occupies)
                    toRemove.Add(i);
            }
            // Remove filled buildings in reverse order
            for (int i = toRemove.Count - 1; i >= 0; i--)
                needsWorkers.RemoveAt(toRemove[i]);
        }

        // Report worker assignments
        var totalOccupied = activeBuildings.Sum(x => x.Item2.AssignedWorkers);
        sb.AppendLine($"Workers:       {totalOccupied}/{game.Resources.People} assigned");
        foreach (var (bDef, building, cell) in activeBuildings)
        {
            if (bDef.Occupies > 0)
                sb.AppendLine($"  ({cell.Row},{cell.Col}) {bDef.Name}: {building.AssignedWorkers}/{bDef.Occupies} workers");
        }

        // ── Production phase (scaled by worker assignment) ──────────────
        var totalProduction = ResourceAmount.Zero;
        foreach (var (bDef, building, _) in activeBuildings)
        {
            ResourceAmount output;
            if (bDef.Occupies > 0)
            {
                // Scale production linearly: base × (assigned / capacity)
                double ratio = (double)building.AssignedWorkers / bDef.Occupies;
                output = bDef.Production.Scale(ratio);
            }
            else
            {
                // Buildings with 0 Occupies always produce at full capacity
                output = bDef.Production;
            }
            totalProduction = totalProduction.Add(output);
        }

        if (!totalProduction.IsEmpty)
        {
            game.Resources = game.Resources.Add(totalProduction);
            sb.AppendLine($"Production:    +{totalProduction}");
        }

        // ── Population cap (sum of settlement capacities) ───────────────
        var popCap = game.Board.AllCells()
            .Where(c => c.Building is not null)
            .Sum(c => c.Building!.PopulationCapacity);

        if (popCap > 0 && game.Resources.People > popCap)
        {
            var excess = game.Resources.People - popCap;
            game.Resources = game.Resources.Set("people", popCap);
            sb.AppendLine($"Population:    capped at {popCap} (excess {excess} lost)");
        }

        // ── Upkeep phase ────────────────────────────────────────────────
        var paidUpkeep = ResourceAmount.Zero;
        var disabledNames = new List<string>();

        foreach (var (bDef, building, _) in activeBuildings)
        {
            if (bDef.Upkeep.IsEmpty) continue;

            if (game.Resources.CanAfford(bDef.Upkeep))
            {
                game.Resources = game.Resources.Subtract(bDef.Upkeep);
                paidUpkeep = paidUpkeep.Add(bDef.Upkeep);
            }
            else
            {
                building.IsActive = false;
                disabledNames.Add(bDef.Name);
            }
        }

        if (!paidUpkeep.IsEmpty)
            sb.AppendLine($"Upkeep:        -{paidUpkeep}");

        if (disabledNames.Count > 0)
            sb.AppendLine($"⚠ Disabled (unpaid upkeep): {string.Join(", ", disabledNames)}");

        // Flux income: earn FluxPerRound each night, capped at MaxFlux
        var fluxBefore = game.Resources.Flux;
        game.Resources = game.Resources.AddFlux(ResourceAmount.FluxPerRound);
        var fluxGained = game.Resources.Flux - fluxBefore;
        sb.AppendLine($"Flux:         +{fluxGained} (now {game.Resources.Flux}/{ResourceAmount.MaxFlux})");
        if (fluxGained < ResourceAmount.FluxPerRound)
            sb.AppendLine($"  (capped — would have gained {ResourceAmount.FluxPerRound}, had {fluxBefore})");

        sb.AppendLine($"Resources now: {game.Resources}");

        // Population summary
        var endPopCap = GetPopulationCap(game);
        sb.AppendLine($"Population:    {game.Resources.People}/{endPopCap}, {totalOccupied} occupied, {game.Resources.People - totalOccupied} available");

        // Burn cards from the land deck at end of each round
        int burnCount = Math.Min(ResourceAmount.DeckBurnPerRound, game.LandDeck.Count);
        if (burnCount > 0)
            game.LandDeck.RemoveRange(0, burnCount);
        sb.AppendLine($"Land deck:     {game.LandDeck.Count} cards remain ({burnCount} burned).");
        if (game.LandDeck.Count == 0)
            sb.AppendLine($"⚠ The land deck is exhausted. No more land cards can be drawn.");

        game.Round++;
        sb.AppendLine($"Round {game.Round} begins.");

        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);
        return (game, sb.ToString().TrimEnd());
    }

    // ── Population ────────────────────────────────────────────────────────

    /// <summary>
    /// Compute total occupied workers from all active buildings on the board (actual assignments).
    /// </summary>
    public int GetOccupiedWorkers(GameState game)
    {
        return game.Board.AllCells()
            .Where(c => c.Building is { IsActive: true })
            .Sum(c => c.Building!.AssignedWorkers);
    }

    /// <summary>
    /// Available workers = total people − occupied workers.
    /// </summary>
    public int GetAvailableWorkers(GameState game) =>
        game.Resources.People - GetOccupiedWorkers(game);

    /// <summary>
    /// Population cap = sum of PopulationCapacity across all settlements on the board.
    /// Returns 0 if no settlements have been placed.
    /// </summary>
    public int GetPopulationCap(GameState game) =>
        game.Board.AllCells()
            .Where(c => c.Building is not null)
            .Sum(c => c.Building!.PopulationCapacity);

    // ── Rendering ───────────────────────────────────────────────────────────

    public string RenderBoard(GameState game)
    {
        var sb = new StringBuilder();
        var occupied = GetOccupiedWorkers(game);
        var available = game.Resources.People - occupied;
        var popCap = GetPopulationCap(game);
        var placedCells = game.Board.AllCells()
            .Where(c => c.Land is not null)
            .ToList();

        sb.AppendLine($"Round {game.Round} | {game.PlayerName} | {game.Resources} | Deck: {game.LandDeck.Count} cards | Population: {game.Resources.People}/{popCap} | Workers: {occupied} occupied, {available} available");
        sb.AppendLine();
        sb.AppendLine($"Board ({placedCells.Count}/{Board.Rows * Board.Cols}):");

        if (placedCells.Count == 0)
        {
            sb.AppendLine("  [next] Empty slot");
            return sb.ToString().TrimEnd();
        }

        for (int i = 0; i < placedCells.Count; i++)
            sb.AppendLine($"  [{i + 1}] {RenderCell(placedCells[i])}");

        if (placedCells.Count < Board.Rows * Board.Cols)
            sb.AppendLine("  [next] Empty slot");

        return sb.ToString().TrimEnd();
    }

    public string RenderHand(GameState game)
    {
        var sb = new StringBuilder();
        if (game.Hand.Count == 0)
        {
            sb.AppendLine($"Hand (0/{ResourceAmount.MaxHandSize} cards) — empty. Use invest or draw_card to get cards.");
            if (game.DiscardPile.Count > 0)
                sb.Append($"Discard pile: {game.DiscardPile.Count} card{(game.DiscardPile.Count != 1 ? "s" : "")}.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"Hand ({game.Hand.Count}/{ResourceAmount.MaxHandSize}):");
        for (int i = 0; i < game.Hand.Count; i++)
        {
            var card = game.Hand[i];
            var def = catalog.Get(card.DefinitionId);
            sb.AppendLine();
            sb.AppendLine($"  [{i}] {def.Name}  (id: {card.InstanceId})");
            sb.AppendLine($"      {def.Description}");

            if (def is BuildingDefinition bDef)
            {
                var playCostStr = bDef.PlayCost.IsEmpty ? "" : $" + {bDef.PlayCost}";
                sb.AppendLine($"      Play cost: {def.FluxCost} Flux{playCostStr}");
                if (bDef.Occupies > 0) sb.AppendLine($"      Workers:   0–{bDef.Occupies} (assigned round-robin at end of round, scales production)");
                if (card is BuildingCard bc && bc.PopulationCapacity > 0)
                    sb.AppendLine($"      Pop cap:   {bc.PopulationCapacity} (max population this settlement supports)");
                if (!bDef.Production.IsEmpty) sb.AppendLine($"      Max prod:  {bDef.Production}/round (at full workers)");
                if (!bDef.Upkeep.IsEmpty)     sb.AppendLine($"      Upkeep:    {bDef.Upkeep}/round");
                var terrain = bDef.AllowedTerrains.Length > 0
                    ? string.Join(", ", bDef.AllowedTerrains)
                    : "any terrain";
                sb.AppendLine($"      Terrain:   {terrain}");
            }
            else if (def is LandDefinition lDef)
            {
                var lCard = (LandCard)card;
                var actualCost = lCard.ComputeFluxCost(def.FluxCost);
                var costNote = actualCost != def.FluxCost ? $" (×{lCard.AccessibilityCost / 10.0:0.0} of base {def.FluxCost})" : "";
                sb.AppendLine($"      Terrain type: {lDef.Terrain}");
                sb.AppendLine($"      Fertility:    ×{lCard.Fertility / 10.0:0.0}  (production bonus when built upon)");
                sb.AppendLine($"      Play cost:    {actualCost} Flux{costNote}");
            }
        }

        if (game.DiscardPile.Count > 0)
        {
            sb.AppendLine();
            sb.Append($"Discard pile: {game.DiscardPile.Count} card{(game.DiscardPile.Count != 1 ? "s" : "")}.");
        }

        return sb.ToString().TrimEnd();
    }

    public string RenderMarket(GameState game)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Your resources: {game.Resources}");
        sb.AppendLine();
        sb.AppendLine("Available investments:");

        foreach (var def in catalog.AllPlayable.OrderBy(d => d.Id))
        {
            var canAfford = game.Resources.CanAfford(def.InvestCost) ? "✓" : "✗";
            sb.AppendLine();
            sb.AppendLine($"  {canAfford} [{def.Id}]  {def.Name}  — Cost: {(def.InvestCost.IsEmpty ? "free" : def.InvestCost.ToString())}");
            sb.AppendLine($"    {def.Description}");

            if (def is BuildingDefinition bDef)
            {
                var playCostStr = bDef.PlayCost.IsEmpty ? "" : $" + {bDef.PlayCost}";
                sb.AppendLine($"    Place cost: {def.FluxCost} Flux{playCostStr}");
                if (bDef.Occupies > 0) sb.AppendLine($"    Workers:    0–{bDef.Occupies} (round-robin, scales production)");
                if (!bDef.Production.IsEmpty) sb.AppendLine($"    Max prod:   {bDef.Production}/round (at full workers)");
                if (!bDef.Upkeep.IsEmpty)     sb.AppendLine($"    Upkeep:     {bDef.Upkeep}/round");
            }
            else
            {
                sb.AppendLine($"    Place cost: {def.FluxCost} Flux");
            }
        }
        return sb.ToString().TrimEnd();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string RenderCell(BoardCell cell)
    {
        var land = cell.Land!;
        var landDef = (LandDefinition)catalog.Get(land.DefinitionId);
        if (cell.Building == null)
            return $"{landDef.Name} | fertility ×{land.Fertility / 10.0:0.0}";

        var bDef = (BuildingDefinition)catalog.Get(cell.Building.DefinitionId);
        var status = cell.Building.IsActive ? "active" : "disabled";
        var details = new List<string> { $"on {landDef.Name}", status };
        if (bDef.Occupies > 0)
            details.Add($"workers {cell.Building.AssignedWorkers}/{bDef.Occupies}");
        if (cell.Building.PopulationCapacity > 0)
            details.Add($"cap {cell.Building.PopulationCapacity}");
        return $"{bDef.Name} | {string.Join(" | ", details)}";
    }

    private static int GetSlotNumber(BoardCell cell) => cell.Row * Board.Cols + cell.Col + 1;

    private static BoardCell FindNextLandPlacementCell(GameState game)
    {
        return game.Board.AllCells()
            .FirstOrDefault(c => !c.IsLocked && c.Land is null)
            ?? throw new InvalidOperationException("No empty board slot is available for a land card.");
    }

    private BoardCell FindNextBuildingPlacementCell(GameState game, BuildingDefinition buildingDefinition)
    {
        return game.Board.AllCells()
            .Where(c =>
                c.Land is not null &&
                CanPlaceBuildingOnCell(game, c, buildingDefinition))
            .OrderByDescending(c => c.Land!.Fertility)
            .ThenBy(c => c.Row)
            .ThenBy(c => c.Col)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No compatible board slot is available for {buildingDefinition.Name}. Play a compatible land card first.");
    }

    private bool CanPlaceBuildingOnCell(GameState game, BoardCell cell, BuildingDefinition buildingDefinition)
    {
        if (cell.Land is null)
            return false;

        var isUpgrade = buildingDefinition.PlacementRequirements.TargetCard is not null;
        if (!isUpgrade && cell.Building is not null)
            return false;

        var landDef = (LandDefinition)catalog.Get(cell.Land.DefinitionId);
        if (!buildingDefinition.CanBuildOn(landDef.Terrain))
            return false;

        return MeetsPlacementRequirements(game, cell, buildingDefinition);
    }

    private void ValidatePlacementRequirements(GameState game, BoardCell cell, BuildingDefinition definition)
    {
        if (!MeetsPlacementRequirements(game, cell, definition, out var error))
            throw new InvalidOperationException(error);
    }

    private bool MeetsPlacementRequirements(GameState game, BoardCell cell, BuildingDefinition definition) =>
        MeetsPlacementRequirements(game, cell, definition, out _);

    private bool MeetsPlacementRequirements(GameState game, BoardCell cell, BuildingDefinition definition, out string error)
    {
        var requirements = definition.PlacementRequirements;
        var cellTags = GetCellTags(cell);

        if (requirements.CellTags.Length > 0 && !requirements.CellTags.Any(tag => cellTags.Contains(tag)))
        {
            error = $"{definition.Name} requires one of these cell tags: {string.Join(", ", requirements.CellTags)}.";
            return false;
        }

        if (requirements.CellTagsAll.Length > 0)
        {
            var missing = requirements.CellTagsAll.Where(tag => !cellTags.Contains(tag)).ToArray();
            if (missing.Length > 0)
            {
                error = $"{definition.Name} requires all of these cell tags: {string.Join(", ", missing)}.";
                return false;
            }
        }

        if (requirements.TargetCard is not null)
        {
            if (cell.Building is null)
            {
                error = $"{definition.Name} requires an existing target building on the chosen cell.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requirements.TargetCard.DefinitionId) &&
                !string.Equals(cell.Building.DefinitionId, requirements.TargetCard.DefinitionId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"{definition.Name} requires {requirements.TargetCard.DefinitionId} on the chosen cell.";
                return false;
            }

            if (cell.Building.Level < requirements.TargetCard.MinLevel)
            {
                error = $"{definition.Name} requires target level {requirements.TargetCard.MinLevel} or higher.";
                return false;
            }
        }

        foreach (var techRequirement in requirements.Technology)
        {
            var currentLevel = game.Technologies.TryGetValue(techRequirement.Key, out var value) ? value : 0;
            if (currentLevel < techRequirement.Value)
            {
                error = $"{definition.Name} requires technology {techRequirement.Key} level {techRequirement.Value}.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private HashSet<string> GetCellTags(BoardCell cell)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (cell.Land is not null)
        {
            var landDef = (LandDefinition)catalog.Get(cell.Land.DefinitionId);
            foreach (var tag in landDef.Tags)
                tags.Add(tag);
            tags.Add($"terrain:{landDef.Terrain.ToString().ToLowerInvariant()}");
        }

        if (cell.Building is not null)
        {
            var buildingDef = (BuildingDefinition)catalog.Get(cell.Building.DefinitionId);
            foreach (var tag in buildingDef.Tags)
                tags.Add(tag);
        }

        return tags;
    }

    private static void ValidateBoardPosition(int row, int col)
    {
        if (row < 0 || row >= Board.Rows || col < 0 || col >= Board.Cols)
            throw new InvalidOperationException(
                $"Position ({row},{col}) is out of bounds. Board is {Board.Rows} rows × {Board.Cols} columns (0-indexed).");
    }
}

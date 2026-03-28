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
            Resources = new ResourceAmount(Food: 8, People: 5, Focus: ResourceAmount.MaxFocus)
        };

        var usableLandIds = catalog.LandCards
            .Where(d => d.Terrain != TerrainType.Wasteland)
            .Select(d => d.Id).ToArray();

        // Starting hand: 3 random useful land cards + 1 Settlement (no wastelands in opening hand)
        for (int i = 0; i < 3; i++)
            game.Hand.Add(LandCard.Create(usableLandIds[Random.Shared.Next(usableLandIds.Length)]));
        game.Hand.Add(BuildingCard.Create("building_settlement"));

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

    // ── Playing cards ───────────────────────────────────────────────────────

    /// <summary>
    /// Play a card from hand onto the board.
    /// Land cards go on empty cells; buildings go on cells that already have land.
    /// </summary>
    public (GameState game, string message) PlayCard(string gameId, string cardInstanceId, int row, int col)
    {
        var game = repo.Load(gameId);
        ValidateBoardPosition(row, col);

        var idx = game.Hand.FindIndex(c => c.InstanceId == cardInstanceId);
        if (idx < 0)
            throw new InvalidOperationException(
                $"Card '{cardInstanceId}' not found in hand. Use get_hand to list available cards.");

        var card = game.Hand[idx];
        var def = catalog.Get(card.DefinitionId);
        var cell = game.Board.GetCell(row, col);

        if (cell.IsLocked)
            throw new InvalidOperationException(
                $"Cell ({row},{col}) is locked. Place a land card on an adjacent cell first to unlock it.");

        // Focus cost — land cards scale by their AccessibilityCost multiplier
        int focusAmount = card is LandCard lc0
            ? lc0.ComputeFocusCost(def.FocusCost)
            : def.FocusCost;
        var focusCost = new ResourceAmount(Focus: focusAmount);
        if (!game.Resources.CanAfford(focusCost))
            throw new InvalidOperationException(
                $"Not enough Focus to play {def.Name}. Need: {focusAmount} Focus, Have: {game.Resources.Focus} Focus.");

        string message;

        if (card is LandCard landCard)
        {
            if (cell.Land != null)
                throw new InvalidOperationException(
                    $"Cell ({row},{col}) already has {catalog.Get(cell.Land.DefinitionId).Name} land. Choose an empty cell.");

            game.Resources = game.Resources.Subtract(focusCost);
            cell.Land = landCard;
            game.Board.UnlockAdjacent(row, col);
            game.Hand.RemoveAt(idx);
            message = $"Placed {def.Name} land at ({row},{col}). Spent: {focusAmount} Focus.";
        }
        else if (card is BuildingCard building)
        {
            if (cell.Land == null)
                throw new InvalidOperationException(
                    $"Cell ({row},{col}) has no land. Play a land card there first.");
            if (cell.Building != null)
                throw new InvalidOperationException(
                    $"Cell ({row},{col}) already has a {catalog.Get(cell.Building.DefinitionId).Name}.");

            var bDef = (BuildingDefinition)def;
            var landDef = (LandDefinition)catalog.Get(cell.Land.DefinitionId);

            if (!bDef.CanBuildOn(landDef.Terrain))
                throw new InvalidOperationException(
                    $"{bDef.Name} cannot be built on {landDef.Terrain}. " +
                    $"Allowed terrain: {(bDef.AllowedTerrains.Length > 0 ? string.Join(", ", bDef.AllowedTerrains) : "any")}.");

            var totalCost = bDef.PlayCost.Add(focusCost);
            if (!game.Resources.CanAfford(totalCost))
                throw new InvalidOperationException(
                    $"Cannot afford {bDef.Name}. Need: {totalCost}, Have: {game.Resources}.");

            game.Resources = game.Resources.Subtract(totalCost);
            cell.Building = building;
            game.Hand.RemoveAt(idx);
            message = $"Built {bDef.Name} at ({row},{col}) on {landDef.Terrain}. Spent: {totalCost}.";
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
    /// Costs DrawCardFocusCost Focus.
    /// </summary>
    public (GameState game, string message) DrawFromDeck(string gameId)
    {
        var game = repo.Load(gameId);

        if (game.Hand.Count >= ResourceAmount.MaxHandSize)
            throw new InvalidOperationException(
                $"Hand is full ({ResourceAmount.MaxHandSize}/{ResourceAmount.MaxHandSize}). Discard a card first using discard_card.");

        if (game.Resources.Focus < ResourceAmount.DrawCardFocusCost)
            throw new InvalidOperationException(
                $"Not enough Focus to draw a card. Need: {ResourceAmount.DrawCardFocusCost} Focus, Have: {game.Resources.Focus} Focus.");

        if (game.LandDeck.Count == 0)
            throw new InvalidOperationException(
                "The land deck is exhausted. No more land cards can be drawn.");

        var drawnId = game.LandDeck[0];
        game.LandDeck.RemoveAt(0);
        var drawn = catalog.Get(drawnId);

        var newCard = LandCard.Create(drawnId);
        game.Hand.Add(newCard);
        game.Resources = game.Resources.Subtract(new ResourceAmount(Focus: ResourceAmount.DrawCardFocusCost));

        game.LastPlayedAt = DateTimeOffset.UtcNow;
        repo.Save(game);

        var message = $"Drew {drawn.Name} from the map deck (id: {newCard.InstanceId}). " +
                      $"Fertility ×{newCard.Fertility / 10.0:0.0}, Play cost ×{newCard.AccessibilityCost / 10.0:0.0}. " +
                      $"Spent: {ResourceAmount.DrawCardFocusCost} Focus.";
        return (game, message);
    }

    // ── Investment / market ─────────────────────────────────────────────────

    public (GameState game, string message) Invest(string gameId, string cardDefinitionId)
    {
        var game = repo.Load(gameId);
        var def = catalog.Get(cardDefinitionId);

        if (game.Hand.Count >= ResourceAmount.MaxHandSize)
            throw new InvalidOperationException(
                $"Hand is full ({ResourceAmount.MaxHandSize}/{ResourceAmount.MaxHandSize}). Discard a card first using discard_card.");

        if (!game.Resources.CanAfford(def.InvestCost))
            throw new InvalidOperationException(
                $"Cannot afford {def.Name}. Need: {def.InvestCost}, Have: {game.Resources}.");

        game.Resources = game.Resources.Subtract(def.InvestCost);

        CardBase newCard = def is LandDefinition
            ? LandCard.Create(cardDefinitionId)
            : BuildingCard.Create(cardDefinitionId);

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
                output = new ResourceAmount(
                    Food: (int)Math.Round(bDef.Production.Food * ratio),
                    People: (int)Math.Round(bDef.Production.People * ratio),
                    Wood: (int)Math.Round(bDef.Production.Wood * ratio));
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
            game.Resources = game.Resources with { People = popCap };
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

        // Focus income: earn FocusPerRound each night, capped at MaxFocus
        var focusBefore = game.Resources.Focus;
        game.Resources = game.Resources.AddFocus(ResourceAmount.FocusPerRound);
        var focusGained = game.Resources.Focus - focusBefore;
        sb.AppendLine($"Focus:         +{focusGained} (now {game.Resources.Focus}/{ResourceAmount.MaxFocus})");
        if (focusGained < ResourceAmount.FocusPerRound)
            sb.AppendLine($"  (capped — would have gained {ResourceAmount.FocusPerRound}, had {focusBefore})");

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
        sb.AppendLine($"Round {game.Round} | {game.PlayerName} | {game.Resources} | Deck: {game.LandDeck.Count} cards | Population: {game.Resources.People}/{popCap} | Workers: {occupied} occupied, {available} available");
        sb.AppendLine();
        sb.Append("      ");
        for (int c = 0; c < Board.Cols; c++) sb.Append($"  [{c}]  ");
        sb.AppendLine();

        for (int r = 0; r < Board.Rows; r++)
        {
            sb.Append($"  [{r}]  ");
            for (int c = 0; c < Board.Cols; c++)
            {
                var cell = game.Board.GetCell(r, c);
                sb.Append(RenderCell(cell).PadRight(7));
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("Legend: FOR=Forest  PLN=Plains  HIL=Hill  BCH=Beach  WST=Wasteland");
        sb.AppendLine("        SET=Settlement  FRM=Farm  LMB=LumberCamp  FSH=FishingCamp  SHP=SheepPasture  !=disabled  ###=locked  ...=empty");
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
                sb.AppendLine($"      Play cost: {def.FocusCost} Focus{playCostStr}");
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
                var actualCost = lCard.ComputeFocusCost(def.FocusCost);
                var costNote = actualCost != def.FocusCost ? $" (×{lCard.AccessibilityCost / 10.0:0.0} of base {def.FocusCost})" : "";
                sb.AppendLine($"      Terrain type: {lDef.Terrain}");
                sb.AppendLine($"      Fertility:    ×{lCard.Fertility / 10.0:0.0}  (production bonus when built upon)");
                sb.AppendLine($"      Play cost:    {actualCost} Focus{costNote}");
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

        foreach (var def in catalog.All.OrderBy(d => d.Id))
        {
            var canAfford = game.Resources.CanAfford(def.InvestCost) ? "✓" : "✗";
            sb.AppendLine();
            sb.AppendLine($"  {canAfford} [{def.Id}]  {def.Name}  — Cost: {(def.InvestCost.IsEmpty ? "free" : def.InvestCost.ToString())}");
            sb.AppendLine($"    {def.Description}");

            if (def is BuildingDefinition bDef)
            {
                var playCostStr = bDef.PlayCost.IsEmpty ? "" : $" + {bDef.PlayCost}";
                sb.AppendLine($"    Place cost: {def.FocusCost} Focus{playCostStr}");
                if (bDef.Occupies > 0) sb.AppendLine($"    Workers:    0–{bDef.Occupies} (round-robin, scales production)");
                if (!bDef.Production.IsEmpty) sb.AppendLine($"    Max prod:   {bDef.Production}/round (at full workers)");
                if (!bDef.Upkeep.IsEmpty)     sb.AppendLine($"    Upkeep:     {bDef.Upkeep}/round");
            }
            else
            {
                sb.AppendLine($"    Place cost: {def.FocusCost} Focus");
            }
        }
        return sb.ToString().TrimEnd();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string RenderCell(BoardCell cell)
    {
        if (cell.IsLocked) return " ### ";
        if (cell.Land == null) return " ... ";

        var landDef = (LandDefinition)catalog.Get(cell.Land.DefinitionId);
        var abbr = landDef.Terrain switch
        {
            TerrainType.Forest => "FOR",
            TerrainType.Plains => "PLN",
            TerrainType.Hill   => "HIL",
            TerrainType.Beach     => "BCH",
            TerrainType.Wasteland => "WST",
            _ => "???"
        };

        if (cell.Building == null) return $" {abbr} ";

        var bDef = (BuildingDefinition)catalog.Get(cell.Building.DefinitionId);
        var bAbbr = bDef.BuildingType switch
        {
            BuildingType.Settlement  => "SET",
            BuildingType.Farm        => "FRM",
            BuildingType.LumberCamp  => "LMB",
            BuildingType.FishingCamp => "FSH",
            BuildingType.SheepPasture => "SHP",
            _ => "???"
        };
        var flag = cell.Building.IsActive ? "" : "!";
        var workers = bDef.Occupies > 0 ? $"{cell.Building.AssignedWorkers}/{bDef.Occupies}" : "";
        var popCap = cell.Building.PopulationCapacity > 0 ? $"p{cell.Building.PopulationCapacity}" : "";
        return $"{bAbbr}{flag}{workers}{popCap}";
    }

    private static void ValidateBoardPosition(int row, int col)
    {
        if (row < 0 || row >= Board.Rows || col < 0 || col >= Board.Cols)
            throw new InvalidOperationException(
                $"Position ({row},{col}) is out of bounds. Board is {Board.Rows} rows × {Board.Cols} columns (0-indexed).");
    }
}

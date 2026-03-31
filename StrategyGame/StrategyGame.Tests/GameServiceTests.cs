using StrategyGame.Core.Catalog;
using StrategyGame.Core.Models;
using StrategyGame.Core.Models.Board;
using StrategyGame.Core.Models.Cards;
using StrategyGame.Core.Services;
using Xunit;

namespace StrategyGame.Tests;

/// <summary>
/// Unit tests for GameService logic using an in-memory repository.
/// </summary>
public sealed class GameServiceTests
{
    private static GameService CreateService() =>
        new(new InMemoryGameRepository(), new CardCatalog());

    // ── StartGame ───────────────────────────────────────────────────────────

    [Fact]
    public void StartGame_CreatesGameWithCorrectPlayerName()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal("Alice", game.PlayerName);
    }

    [Fact]
    public void StartGame_InitialRoundIsOne()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(1, game.Round);
    }

    [Fact]
    public void StartGame_InitialResourcesAreCorrect()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(8, game.Resources.Food);
        Assert.Equal(5, game.Resources.People);
        Assert.Equal(ResourceAmount.MaxFlux, game.Resources.Flux);
    }

    [Fact]
    public void StartGame_HandHasFourCards()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(4, game.Hand.Count);
    }

    [Fact]
    public void StartGame_HandHasThreeLandCardsAndOneBuilding()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(3, game.Hand.OfType<LandCard>().Count());
        Assert.Single(game.Hand.OfType<BuildingCard>());
    }

    [Fact]
    public void StartGame_BuildingCardIsSettlement()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var building = game.Hand.OfType<BuildingCard>().Single();
        Assert.Equal("building_settlement", building.DefinitionId);
    }

    [Fact]
    public void StartGame_GameIdIsAssigned()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.False(string.IsNullOrEmpty(game.GameId));
    }

    [Fact]
    public void StartGame_CanBeLoadedBack()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var loaded = svc.LoadGame(game.GameId);
        Assert.Equal(game.GameId, loaded.GameId);
        Assert.Equal("Alice", loaded.PlayerName);
    }

    [Fact]
    public void ListGames_IncludesStartedGame()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var list = svc.ListGames();
        Assert.Contains(list, s => s.GameId == game.GameId && s.PlayerName == "Alice");
    }

    // ── LandCard stats ──────────────────────────────────────────────────────

    [Fact]
    public void LandCard_Create_FertilityInRange()
    {
        var catalog = new CardCatalog();
        var def = (LandDefinition)catalog.Get("land_plains");
        for (int i = 0; i < 200; i++)
        {
            var card = LandCard.Create(def);
            Assert.InRange(card.Fertility, def.StatRanges.Fertility.Min, def.StatRanges.Fertility.Max);
        }
    }

    [Fact]
    public void LandCard_Create_FluxCostBakedFromRange()
    {
        var catalog = new CardCatalog();
        var def = (LandDefinition)catalog.Get("land_plains");
        for (int i = 0; i < 200; i++)
        {
            var card = LandCard.Create(def);
            // FluxCost = round(base × scale/10), scale in [min..max]
            var minCost = Math.Max(1, (int)Math.Round(def.FluxCost * def.StatRanges.FluxScale.Min / 10.0, MidpointRounding.AwayFromZero));
            var maxCost = Math.Max(1, (int)Math.Round(def.FluxCost * def.StatRanges.FluxScale.Max / 10.0, MidpointRounding.AwayFromZero));
            Assert.InRange(card.FluxCost, minCost, maxCost);
        }
    }

    [Fact]
    public void LandCard_Create_FluxCostDeterministicWithSeed()
    {
        var catalog = new CardCatalog();
        var def = (LandDefinition)catalog.Get("land_plains");
        var rng = new Random(42);
        var card = LandCard.Create(def, rng);
        // With a seeded RNG the result is deterministic
        var rng2 = new Random(42);
        var card2 = LandCard.Create(def, rng2);
        Assert.Equal(card.Fertility, card2.Fertility);
        Assert.Equal(card.FluxCost, card2.FluxCost);
    }

    [Fact]
    public void StartGame_LandCardsHaveRolledStats()
    {
        var catalog = new CardCatalog();
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        foreach (var card in game.Hand.OfType<LandCard>())
        {
            var def = (LandDefinition)catalog.Get(card.DefinitionId);
            Assert.InRange(card.Fertility, def.StatRanges.Fertility.Min, def.StatRanges.Fertility.Max);
            Assert.True(card.FluxCost >= 1);
        }
    }

    [Fact]
    public void DrawFromDeck_LandCardHasRolledStats()
    {
        var catalog = new CardCatalog();
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        var drawn = updated.Hand.Last() as LandCard;
        Assert.NotNull(drawn);
        var def = (LandDefinition)catalog.Get(drawn.DefinitionId);
        Assert.InRange(drawn.Fertility, def.StatRanges.Fertility.Min, def.StatRanges.Fertility.Max);
        Assert.True(drawn.FluxCost >= 1);
    }

    [Fact]
    public void PlayCard_LandCard_FluxCostUsesCardFluxCost()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Create a card with a known FluxCost of 2
        var cheapLand = new LandCard { DefinitionId = "land_plains", Fertility = 10, FluxCost = 2 };
        game.Hand.Add(cheapLand);
        var targetCell = game.Board.GetCell(0, 4);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var fluxBefore = game.Resources.Flux;
        var (result, _) = svc.PlayCard(game.GameId, cheapLand.InstanceId, 0, 4);
        Assert.Equal(fluxBefore - 2, result.Resources.Flux);
    }

    // ── DrawFromDeck ────────────────────────────────────────────────────────

    [Fact]
    public void DrawFromDeck_AddsLandCardToHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var handSizeBefore = game.Hand.Count;
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        Assert.Equal(handSizeBefore + 1, updated.Hand.Count);
        Assert.IsType<LandCard>(updated.Hand.Last());
    }

    [Fact]
    public void DrawFromDeck_ConsumesFlux()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var fluxBefore = game.Resources.Flux;
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        Assert.Equal(fluxBefore - ResourceAmount.DrawCardFluxCost, updated.Resources.Flux);
    }

    [Fact]
    public void DrawFromDeck_ReturnsDescriptiveMessage()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, message) = svc.DrawFromDeck(game.GameId);
        Assert.Contains("Drew", message);
        Assert.Contains("Flux", message);
    }

    [Fact]
    public void DrawFromDeck_ThrowsWhenNoFlux()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Drain all flux
        game.Resources = game.Resources with { Flux = 0 };
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() => svc.DrawFromDeck(game.GameId));
    }

    // ── PlayCard (land) ─────────────────────────────────────────────────────

    [Fact]
    public void PlayCard_LandCard_PlacesOnEmptyCell()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var (updated, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        Assert.NotNull(updated.Board.GetCell(0, 0).Land);
        Assert.Equal(landCard.DefinitionId, updated.Board.GetCell(0, 0).Land!.DefinitionId);
    }

    [Fact]
    public void PlayCard_AutoPlacement_PlacesLandInFirstAvailableSlot()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();

        var (updated, message) = svc.PlayCard(game.GameId, landCard.InstanceId);

        Assert.NotNull(updated.Board.GetCell(0, 0).Land);
        Assert.Equal(landCard.DefinitionId, updated.Board.GetCell(0, 0).Land!.DefinitionId);
        Assert.Contains("slot 1", message);
    }

    [Fact]
    public void PlayCard_LandCard_RemovesFromHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var handSizeBefore = game.Hand.Count;
        var (updated, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        Assert.Equal(handSizeBefore - 1, updated.Hand.Count);
        Assert.DoesNotContain(updated.Hand, c => c.InstanceId == landCard.InstanceId);
    }

    [Fact]
    public void PlayCard_LandCard_ConsumesFlux()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var fluxBefore = game.Resources.Flux;
        var (updated, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        Assert.Equal(fluxBefore - landCard.FluxCost, updated.Resources.Flux);
    }

    [Fact]
    public void PlayCard_LandCard_FailsOnOccupiedCell()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var cards = game.Hand.OfType<LandCard>().Take(2).ToList();
        svc.PlayCard(game.GameId, cards[0].InstanceId, 0, 0);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, cards[1].InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_FailsWithUnknownCardId()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, "nonexistent", 0, 0));
    }

    [Fact]
    public void PlayCard_FailsWhenNotEnoughFlux()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Flux = 0 };
        repo.Save(game);
        var landCard = game.Hand.OfType<LandCard>().First();
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_Land_BoardStaysAtFourSlots()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Inject 3 terrain cards into hand
        game.Hand.Add(new LandCard { DefinitionId = "land_forest" });
        game.Hand.Add(new LandCard { DefinitionId = "land_plains" });
        game.Hand.Add(new LandCard { DefinitionId = "land_hill" });
        repo.Save(game);

        var lands = game.Hand.OfType<LandCard>().Take(3).ToList();
        foreach (var land in lands)
            svc.PlayCard(game.GameId, land.InstanceId);

        var updated = svc.LoadGame(game.GameId);
        var allCells = updated.Board.AllCells().Where(c => !c.IsLocked).ToList();
        Assert.Equal(4, allCells.Count);
        Assert.Equal(3, allCells.Count(c => c.HasTerrain));
        Assert.Single(allCells, c => c.IsEmpty);
    }

    // ── PlayCard (building) ─────────────────────────────────────────────────

    [Fact]
    public void PlayCard_BuildingCard_FailsWithoutLandOnCell()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var building = game.Hand.OfType<BuildingCard>().First();
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, building.InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_Settlement_SucceedsOnAnyTerrain()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var (afterLand, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        var settlement = afterLand.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        var (afterBuilding, _) = svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);
        Assert.NotNull(afterBuilding.Board.GetCell(0, 0).Building);
        Assert.Equal("building_settlement", afterBuilding.Board.GetCell(0, 0).Building!.DefinitionId);
    }

    [Fact]
    public void PlayCard_AutoPlacement_BuildingUsesNextCompatibleLandSlot()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Wood = 10 };

        var plains = new LandCard { DefinitionId = "land_plains" };
        var beach = new LandCard { DefinitionId = "land_beach" };
        var camp = new BuildingCard { DefinitionId = "building_fishing_camp" };
        game.Hand.Add(plains);
        game.Hand.Add(beach);
        game.Hand.Add(camp);
        repo.Save(game);

        svc.PlayCard(game.GameId, plains.InstanceId);
        svc.PlayCard(game.GameId, beach.InstanceId);
        var (updated, message) = svc.PlayCard(game.GameId, camp.InstanceId);

        Assert.Null(updated.Board.GetCell(0, 0).Building);
        Assert.NotNull(updated.Board.GetCell(0, 1).Building);
        Assert.Equal("building_fishing_camp", updated.Board.GetCell(0, 1).Building!.DefinitionId);
        Assert.Contains("slot 2", message);
    }

    [Fact]
    public void PlayCard_Farm_SucceedsOnPlains()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Wood = 10 }; // Farm costs 10 Wood to place
        var plains = new LandCard { DefinitionId = "land_plains" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(plains);
        game.Hand.Add(farm);
        var targetCell = game.Board.GetCell(2, 2);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, plains.InstanceId, 2, 2);
        var (result, _) = svc.PlayCard(game.GameId, farm.InstanceId, 2, 2);
        Assert.NotNull(result.Board.GetCell(2, 2).Building);
        Assert.Equal("building_farm", result.Board.GetCell(2, 2).Building!.DefinitionId);
    }

    [Fact]
    public void PlayCard_Farm_FailsOnForest()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Seed cards directly so we don't exhaust People via Invest
        var forest = new LandCard { DefinitionId = "land_forest" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(forest);
        game.Hand.Add(farm);
        var targetCell = game.Board.GetCell(2, 2);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, forest.InstanceId, 2, 2);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, farm.InstanceId, 2, 2));
    }

    [Fact]
    public void PlayCard_LumberCamp_SucceedsOnForest()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var forest = new LandCard { DefinitionId = "land_forest" };
        var camp = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(forest);
        game.Hand.Add(camp);
        var targetCell = game.Board.GetCell(3, 3);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, forest.InstanceId, 3, 3);
        var (result, _) = svc.PlayCard(game.GameId, camp.InstanceId, 3, 3);
        Assert.NotNull(result.Board.GetCell(3, 3).Building);
        Assert.Equal("building_lumber_camp", result.Board.GetCell(3, 3).Building!.DefinitionId);
    }

    [Fact]
    public void PlayCard_LumberCamp_FailsOnPlains()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var plains = new LandCard { DefinitionId = "land_plains" };
        var camp = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(plains);
        game.Hand.Add(camp);
        var targetCell = game.Board.GetCell(3, 3);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, plains.InstanceId, 3, 3);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, camp.InstanceId, 3, 3));
    }

    [Fact]
    public void PlayCard_Farm_FailsWithoutEnoughWood()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // No Wood in starting resources
        var plains = new LandCard { DefinitionId = "land_plains" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(plains);
        game.Hand.Add(farm);
        var targetCell = game.Board.GetCell(2, 2);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, plains.InstanceId, 2, 2);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, farm.InstanceId, 2, 2));
    }

    [Fact]
    public void PlayCard_Farm_SucceedsWithEnoughWood()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Wood = 10 };
        var plains = new LandCard { DefinitionId = "land_plains" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(plains);
        game.Hand.Add(farm);
        var targetCell = game.Board.GetCell(2, 2);
        targetCell.IsLocked = false;
        targetCell.Land = LandCard.CreateEmpty();
        repo.Save(game);
        var (afterLand, _) = svc.PlayCard(game.GameId, plains.InstanceId, 2, 2);
        var (result, _) = svc.PlayCard(game.GameId, farm.InstanceId, 2, 2);
        Assert.NotNull(result.Board.GetCell(2, 2).Building);
        Assert.Equal(0, result.Resources.Wood); // 10 wood spent
    }

    [Fact]
    public void PlayCard_FishingCamp_SucceedsOnBeach()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var beach = new LandCard { DefinitionId = "land_beach" };
        var camp = new BuildingCard { DefinitionId = "building_fishing_camp" };
        game.Hand.Add(beach);
        game.Hand.Add(camp);
        repo.Save(game);
        svc.PlayCard(game.GameId, beach.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, camp.InstanceId, 0, 0);
        Assert.Equal("building_fishing_camp", result.Board.GetCell(0, 0).Building!.DefinitionId);
    }

    [Fact]
    public void PlayCard_FishingCamp_FailsOnNonBeach()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var plains = new LandCard { DefinitionId = "land_plains" };
        var camp = new BuildingCard { DefinitionId = "building_fishing_camp" };
        game.Hand.Add(plains);
        game.Hand.Add(camp);
        repo.Save(game);
        svc.PlayCard(game.GameId, plains.InstanceId, 0, 0);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, camp.InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_SheepPasture_SucceedsOnHill()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var hill = new LandCard { DefinitionId = "land_hill" };
        var pasture = new BuildingCard { DefinitionId = "building_sheep_pasture" };
        game.Hand.Add(hill);
        game.Hand.Add(pasture);
        repo.Save(game);
        svc.PlayCard(game.GameId, hill.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, pasture.InstanceId, 0, 0);
        Assert.Equal("building_sheep_pasture", result.Board.GetCell(0, 0).Building!.DefinitionId);
    }

    [Fact]
    public void PlayCard_SheepPasture_FailsOnNonHill()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var beach = new LandCard { DefinitionId = "land_beach" };
        var pasture = new BuildingCard { DefinitionId = "building_sheep_pasture" };
        game.Hand.Add(beach);
        game.Hand.Add(pasture);
        repo.Save(game);
        svc.PlayCard(game.GameId, beach.InstanceId, 0, 0);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, pasture.InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_Settlement_FailsIfCellAlreadyHasBuilding()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        var game2 = svc.LoadGame(game.GameId);
        var settlement = game2.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);
        // Add a second settlement to hand directly and try to place it in the same spot
        var game3 = svc.LoadGame(game.GameId);
        var secondSettlement = new BuildingCard { DefinitionId = "building_settlement" };
        game3.Hand.Add(secondSettlement);
        repo.Save(game3);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, secondSettlement.InstanceId, 0, 0));
    }

    // ── EndRound ────────────────────────────────────────────────────────────

    [Fact]
    public void EndRound_IncrementsRound()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (updated, _) = svc.EndRound(game.GameId);
        Assert.Equal(2, updated.Round);
    }

    [Fact]
    public void EndRound_RestoresFlux()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Spend some flux first
        game.Resources = game.Resources with { Flux = 0 };
        repo.Save(game);
        var (updated, _) = svc.EndRound(game.GameId);
        Assert.Equal(ResourceAmount.FluxPerRound, updated.Resources.Flux);
    }

    [Fact]
    public void EndRound_FluxIsCappedAtMax()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice"); // starts with MaxFlux
        var (updated, _) = svc.EndRound(game.GameId);
        Assert.Equal(ResourceAmount.MaxFlux, updated.Resources.Flux);
    }

    [Fact]
    public void EndRound_SummaryContainsRoundInfo()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, summary) = svc.EndRound(game.GameId);
        Assert.Contains("Round 1", summary);
        Assert.Contains("Flux", summary);
    }

    [Fact]
    public void EndRound_ActiveSettlementProducesAndConsumes()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        // Place a land then the settlement
        var landCard = game.Hand.OfType<LandCard>().First();
        var (afterLand, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        var settlement = afterLand.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        var (afterBuild, _) = svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);

        var peopleBeforeRound = afterBuild.Resources.People;
        var foodBeforeRound = afterBuild.Resources.Food;
        var (afterRound, _) = svc.EndRound(game.GameId);

        // Settlement: +2 People production, -1 Food upkeep
        Assert.Equal(peopleBeforeRound + 2, afterRound.Resources.People);
        Assert.Equal(foodBeforeRound - 1, afterRound.Resources.Food);
    }

    [Fact]
    public void EndRound_DisablesBuildingWhenCantPayUpkeep()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Place a land then settlement
        var landCard = game.Hand.OfType<LandCard>().First();
        var (afterLand, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        var settlement = afterLand.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);
        // Remove all food so upkeep can't be paid
        var current = svc.LoadGame(game.GameId);
        current.Resources = current.Resources with { Food = 0 };
        repo.Save(current);

        var (afterRound, summary) = svc.EndRound(game.GameId);

        var cell = afterRound.Board.GetCell(0, 0);
        Assert.NotNull(cell.Building);
        Assert.False(cell.Building!.IsActive);
        Assert.Contains("Disabled", summary);
    }

    // ── Hand limit ──────────────────────────────────────────────────────────

    [Fact]
    public void DrawFromDeck_ThrowsWhenHandFull()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Fill hand to max
        while (game.Hand.Count < ResourceAmount.MaxHandSize)
            game.Hand.Add(new LandCard { DefinitionId = "land_plains" });
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() => svc.DrawFromDeck(game.GameId));
    }

    // ── Discard ─────────────────────────────────────────────────────────────

    [Fact]
    public void DiscardCard_MovesCardToDiscardPile()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var card = game.Hand[0];
        var (updated, _) = svc.DiscardCard(game.GameId, card.InstanceId);
        Assert.DoesNotContain(updated.Hand, c => c.InstanceId == card.InstanceId);
        Assert.Contains(updated.DiscardPile, c => c.InstanceId == card.InstanceId);
    }

    [Fact]
    public void DiscardCard_ReducesHandSize()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var sizeBefore = game.Hand.Count;
        svc.DiscardCard(game.GameId, game.Hand[0].InstanceId);
        var updated = svc.LoadGame(game.GameId);
        Assert.Equal(sizeBefore - 1, updated.Hand.Count);
    }

    [Fact]
    public void DiscardCard_ThrowsWhenCardNotInHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Throws<InvalidOperationException>(() =>
            svc.DiscardCard(game.GameId, "nonexistent"));
    }

    [Fact]
    public void DiscardCard_AllowsDrawAfterBeingFull()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        while (game.Hand.Count < ResourceAmount.MaxHandSize)
            game.Hand.Add(new LandCard { DefinitionId = "land_plains" });
        repo.Save(game);
        // Discard to make room
        svc.DiscardCard(game.GameId, game.Hand[0].InstanceId);
        // Now draw should succeed
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        Assert.Equal(ResourceAmount.MaxHandSize, updated.Hand.Count);
    }

    // ── Land deck ───────────────────────────────────────────────────────────

    [Fact]
    public void StartGame_LandDeckHas500Cards()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(ResourceAmount.LandDeckSize, game.LandDeck.Count);
    }

    [Fact]
    public void DrawFromDeck_RemovesFromDeckFront()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var deckBefore = game.LandDeck.Count;
        svc.DrawFromDeck(game.GameId);
        var updated = svc.LoadGame(game.GameId);
        Assert.Equal(deckBefore - 1, updated.LandDeck.Count);
    }

    [Fact]
    public void DrawFromDeck_ThrowsWhenDeckEmpty()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.LandDeck.Clear();
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() => svc.DrawFromDeck(game.GameId));
    }

    [Fact]
    public void EndRound_Burns15CardsFromDeck()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var deckBefore = game.LandDeck.Count;
        svc.EndRound(game.GameId);
        var updated = svc.LoadGame(game.GameId);
        Assert.Equal(deckBefore - ResourceAmount.DeckBurnPerRound, updated.LandDeck.Count);
    }

    [Fact]
    public void EndRound_BurnsSummaryContainsDeckInfo()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, summary) = svc.EndRound(game.GameId);
        Assert.Contains("Land deck:", summary);
        Assert.Contains("burned", summary);
    }

    [Fact]
    public void EndRound_BurnsOnlyRemainingCardsWhenDeckAlmostEmpty()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.LandDeck = game.LandDeck.Take(5).ToList(); // only 5 left
        repo.Save(game);
        var (updated, summary) = svc.EndRound(game.GameId);
        Assert.Empty(updated.LandDeck);
        Assert.Contains("exhausted", summary);
    }

    // ── Locked cells ────────────────────────────────────────────────────────

    [Fact]
    public void Board_StartsWith4UnlockedCells()
    {
        var board = new StrategyGame.Core.Models.Board.Board();
        var unlocked = board.AllCells().Count(c => !c.IsLocked);
        Assert.Equal(Board.StartRows * Board.StartCols, unlocked);
    }

    [Fact]
    public void Board_StartingCellsHaveEmptyLand()
    {
        var board = new StrategyGame.Core.Models.Board.Board();
        var emptyCells = board.AllCells().Count(c => c.IsEmpty);
        Assert.Equal(Board.StartRows * Board.StartCols, emptyCells);
    }

    [Fact]
    public void Board_HasCorrectLockedCellCount()
    {
        var board = new StrategyGame.Core.Models.Board.Board();
        var locked = board.AllCells().Count(c => c.IsLocked);
        Assert.Equal(Board.Rows * Board.Cols - Board.StartRows * Board.StartCols, locked);
    }

    [Fact]
    public void PlayCard_ThrowsWhenCellIsLocked()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var land = new LandCard { DefinitionId = "land_plains" };
        game.Hand.Add(land);
        // (0, 4) is outside the starting zone → locked
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, land.InstanceId, 0, 4));
    }

    // ── Wasteland ───────────────────────────────────────────────────────────

    [Fact]
    public void PlayCard_Settlement_FailsOnWasteland()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var wasteland = new LandCard { DefinitionId = "land_wasteland" };
        var settlement = new BuildingCard { DefinitionId = "building_settlement" };
        game.Hand.Add(wasteland);
        game.Hand.Add(settlement);
        repo.Save(game);
        svc.PlayCard(game.GameId, wasteland.InstanceId, 0, 0);
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0));
    }

    [Fact]
    public void PlayCard_Wasteland_CanBePlacedOnBoard()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var wasteland = new LandCard { DefinitionId = "land_wasteland" };
        game.Hand.Add(wasteland);
        repo.Save(game);
        var (updated, msg) = svc.PlayCard(game.GameId, wasteland.InstanceId, 0, 0);
        Assert.NotNull(updated.Board.GetCell(0, 0).Land);
        Assert.Contains("Wasteland", msg);
    }

    [Fact]
    public void StartGame_DeckContainsWastelands()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Contains(game.LandDeck, id => id == "land_wasteland");
    }

    [Fact]
    public void StartGame_OpeningHandHasNoWastelands()
    {
        var svc = CreateService();
        // Run several times to be confident
        for (int i = 0; i < 20; i++)
        {
            var game = svc.StartGame("Alice");
            Assert.DoesNotContain(game.Hand.OfType<LandCard>(),
                c => c.DefinitionId == "land_wasteland");
        }
    }

    // ── Occupation / Population ─────────────────────────────────────────────

    [Fact]
    public void PlayCard_Building_DoesNotConsumePeople()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        var peopleBefore = game.Resources.People;
        // Place land then lumber camp (occupies 2 workers)
        var forest = new LandCard { DefinitionId = "land_forest" };
        var camp = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(forest);
        game.Hand.Add(camp);
        game.Board.GetCell(0, 0).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, forest.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, camp.InstanceId, 0, 0);
        // People count should NOT decrease — workers are occupied, not consumed
        Assert.Equal(peopleBefore, result.Resources.People);
    }

    [Fact]
    public void GetOccupiedWorkers_ZeroBeforeEndRound()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        Assert.Equal(0, svc.GetOccupiedWorkers(game));
        // Place a lumber camp — workers not yet assigned (happens at EndRound)
        var forest = new LandCard { DefinitionId = "land_forest" };
        var camp = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(forest);
        game.Hand.Add(camp);
        game.Board.GetCell(0, 0).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, forest.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, camp.InstanceId, 0, 0);
        Assert.Equal(0, svc.GetOccupiedWorkers(result)); // Not assigned until EndRound
    }

    [Fact]
    public void EndRound_AssignsWorkersRoundRobin()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Place two lumber camps (each needs 2 workers), start with 5 people
        var forest1 = new LandCard { DefinitionId = "land_forest" };
        var forest2 = new LandCard { DefinitionId = "land_forest" };
        var camp1 = new BuildingCard { DefinitionId = "building_lumber_camp" };
        var camp2 = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(forest1);
        game.Hand.Add(camp1);
        game.Hand.Add(forest2);
        game.Hand.Add(camp2);
        game.Board.GetCell(0, 1).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, forest1.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, camp1.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, forest2.InstanceId, 0, 1);
        svc.PlayCard(game.GameId, camp2.InstanceId, 0, 1);
        // 5 people, 2 camps each needing 2 → camp1 gets 2, camp2 gets 2, 1 leftover
        var (result, _) = svc.EndRound(game.GameId);
        var b1 = result.Board.GetCell(0, 0).Building!;
        var b2 = result.Board.GetCell(0, 1).Building!;
        Assert.Equal(2, b1.AssignedWorkers);
        Assert.Equal(2, b2.AssignedWorkers);
        // +2 people from settlement production not present, so total occupied = 4, available = people-4
        Assert.Equal(4, svc.GetOccupiedWorkers(result));
    }

    [Fact]
    public void GetAvailableWorkers_ReturnsCorrectAfterEndRound()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        Assert.Equal(5, svc.GetAvailableWorkers(game));
        // Place a lumber camp (needs 2 workers)
        var forest = new LandCard { DefinitionId = "land_forest" };
        var camp = new BuildingCard { DefinitionId = "building_lumber_camp" };
        game.Hand.Add(forest);
        game.Hand.Add(camp);
        game.Board.GetCell(0, 0).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, forest.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, camp.InstanceId, 0, 0);
        var (result, _) = svc.EndRound(game.GameId);
        Assert.Equal(2, svc.GetOccupiedWorkers(result));
        Assert.Equal(result.Resources.People - 2, svc.GetAvailableWorkers(result));
    }

    [Fact]
    public void PlayCard_Building_SucceedsEvenWithNoWorkers()
    {
        // Buildings can always be placed — workers are distributed at EndRound
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { People = 0, Wood = 10 };
        var plains = new LandCard { DefinitionId = "land_plains" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(plains);
        game.Hand.Add(farm);
        game.Board.GetCell(0, 0).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, plains.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, farm.InstanceId, 0, 0);
        Assert.NotNull(result.Board.GetCell(0, 0).Building);
    }

    [Fact]
    public void EndRound_ScalesProductionByWorkerAssignment()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Place a farm (occupies 3, produces 6 food at full)
        game.Resources = game.Resources with { People = 2, Wood = 10 };
        var plains = new LandCard { DefinitionId = "land_plains" };
        var farm = new BuildingCard { DefinitionId = "building_farm" };
        game.Hand.Add(plains);
        game.Hand.Add(farm);
        game.Board.GetCell(0, 0).IsLocked = false;
        repo.Save(game);
        svc.PlayCard(game.GameId, plains.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, farm.InstanceId, 0, 0);
        var foodBefore = svc.LoadGame(game.GameId).Resources.Food;
        var (result, summary) = svc.EndRound(game.GameId);
        // 2 people / 3 capacity = 2/3 ratio → 6 * 2/3 = 4 food
        Assert.Equal(2, result.Board.GetCell(0, 0).Building!.AssignedWorkers);
        Assert.Equal(foodBefore + 4, result.Resources.Food);
    }

    [Fact]
    public void PlayCard_Settlement_SucceedsWithZeroOccupies()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Settlement occupies 0 workers — should always succeed
        game.Resources = game.Resources with { People = 0 };
        var land = game.Hand.OfType<LandCard>().First();
        var settlement = game.Hand.OfType<BuildingCard>().First(c => c.DefinitionId == "building_settlement");
        repo.Save(game);
        svc.PlayCard(game.GameId, land.InstanceId, 0, 0);
        var (result, _) = svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);
        Assert.NotNull(result.Board.GetCell(0, 0).Building);
    }

    [Fact]
    public void EndRound_SummaryContainsPopulationInfo()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, summary) = svc.EndRound(game.GameId);
        Assert.Contains("Population:", summary);
        Assert.Contains("available", summary);
    }

    // ── Population Capacity ─────────────────────────────────────────────────

    [Fact]
    public void BuildingCard_Create_Settlement_RollsPopulationCapacity()
    {
        var card = BuildingCard.Create("building_settlement");
        Assert.InRange(card.PopulationCapacity, 10, 40);
    }

    [Fact]
    public void BuildingCard_Create_NonSettlement_HasZeroPopulationCapacity()
    {
        var card = BuildingCard.Create("building_farm");
        Assert.Equal(0, card.PopulationCapacity);
    }

    [Fact]
    public void GetPopulationCap_ReturnsZeroWithNoSettlements()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        Assert.Equal(0, svc.GetPopulationCap(game));
    }

    [Fact]
    public void GetPopulationCap_SumsSettlementCapacities()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");

        var catalog = new CardCatalog();

        // Place two settlements with known capacities
        var land1 = LandCard.Create((LandDefinition)catalog.Get("land_plains"));
        var land2 = LandCard.Create((LandDefinition)catalog.Get("land_forest"));
        var set1 = new BuildingCard { DefinitionId = "building_settlement", PopulationCapacity = 20 };
        var set2 = new BuildingCard { DefinitionId = "building_settlement", PopulationCapacity = 15 };
        game.Hand.AddRange(new CardBase[] { land1, land2, set1, set2 });
        game.Resources = game.Resources with { Flux = ResourceAmount.MaxFlux * 2 };
        repo.Save(game);

        svc.PlayCard(game.GameId, land1.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, set1.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, land2.InstanceId, 0, 1);
        svc.PlayCard(game.GameId, set2.InstanceId, 0, 1);

        var updated = repo.Load(game.GameId);
        Assert.Equal(35, svc.GetPopulationCap(updated));
    }

    [Fact]
    public void EndRound_CapsPopulationAtSettlementCapacity()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");

        // Place a settlement with low capacity
        var land = LandCard.Create((LandDefinition)new CardCatalog().Get("land_plains"));
        var settlement = new BuildingCard { DefinitionId = "building_settlement", PopulationCapacity = 3 };
        game.Hand.AddRange(new CardBase[] { land, settlement });
        repo.Save(game);

        svc.PlayCard(game.GameId, land.InstanceId, 0, 0);
        svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);

        // People starts at 5, cap is 3 — should be capped after EndRound
        var (result, summary) = svc.EndRound(game.GameId);
        Assert.True(result.Resources.People <= 3, $"People should be capped at 3, was {result.Resources.People}");
        Assert.Contains("capped", summary);
    }

    [Fact]
    public void StartGame_SettlementInHand_HasPopulationCapacity()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var settlement = game.Hand.OfType<BuildingCard>()
            .FirstOrDefault(c => c.DefinitionId == "building_settlement");
        Assert.NotNull(settlement);
        Assert.InRange(settlement!.PopulationCapacity, 10, 40);
    }

    // ── DeleteGame ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteGame_RemovesGame()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        svc.DeleteGame(game.GameId);
        Assert.Empty(svc.ListGames());
    }

    [Fact]
    public void DeleteGame_NonExistentThrows()
    {
        var svc = CreateService();
        Assert.Throws<InvalidOperationException>(() => svc.DeleteGame("nonexistent"));
    }

    [Fact]
    public void DeleteGame_LoadAfterDeleteThrows()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        svc.DeleteGame(game.GameId);
        Assert.Throws<InvalidOperationException>(() => svc.LoadGame(game.GameId));
    }
}

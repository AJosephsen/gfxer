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
        Assert.Equal(ResourceAmount.MaxFocus, game.Resources.Focus);
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
        for (int i = 0; i < 200; i++)
        {
            var card = LandCard.Create("land_plains");
            Assert.InRange(card.Fertility, 5, 15);
        }
    }

    [Fact]
    public void LandCard_Create_AccessibilityCostInRange()
    {
        for (int i = 0; i < 200; i++)
        {
            var card = LandCard.Create("land_plains");
            Assert.InRange(card.AccessibilityCost, 5, 12);
        }
    }

    [Fact]
    public void LandCard_ComputeFocusCost_ScalesWithAccessibility()
    {
        var cheap  = new LandCard { DefinitionId = "land_plains", Fertility = 10, AccessibilityCost = 5  };
        var normal = new LandCard { DefinitionId = "land_plains", Fertility = 10, AccessibilityCost = 10 };
        var pricey = new LandCard { DefinitionId = "land_plains", Fertility = 10, AccessibilityCost = 12 };
        Assert.Equal(2, cheap.ComputeFocusCost(3));   // round(3 × 0.5) = 2
        Assert.Equal(3, normal.ComputeFocusCost(3));  // round(3 × 1.0) = 3
        Assert.Equal(4, pricey.ComputeFocusCost(3));  // round(3 × 1.2) = 4
    }

    [Fact]
    public void StartGame_LandCardsHaveRolledStats()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        foreach (var card in game.Hand.OfType<LandCard>())
        {
            Assert.InRange(card.Fertility, 5, 15);
            Assert.InRange(card.AccessibilityCost, 5, 12);
        }
    }

    [Fact]
    public void DrawFromDeck_LandCardHasRolledStats()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        var drawn = updated.Hand.Last() as LandCard;
        Assert.NotNull(drawn);
        Assert.InRange(drawn.Fertility, 5, 15);
        Assert.InRange(drawn.AccessibilityCost, 5, 12);
    }

    [Fact]
    public void PlayCard_LandCard_FocusCostReflectsAccessibility()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Plant a card with known AccessibilityCost = 5 → cost = round(3 × 0.5) = 2
        var cheapLand = new LandCard { DefinitionId = "land_plains", Fertility = 10, AccessibilityCost = 5 };
        game.Hand.Add(cheapLand);
        game.Board.GetCell(0, 4).IsLocked = false;
        repo.Save(game);
        var focusBefore = game.Resources.Focus;
        var (result, _) = svc.PlayCard(game.GameId, cheapLand.InstanceId, 0, 4);
        Assert.Equal(focusBefore - 2, result.Resources.Focus);
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
    public void DrawFromDeck_ConsumesFocus()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var focusBefore = game.Resources.Focus;
        var (updated, _) = svc.DrawFromDeck(game.GameId);
        Assert.Equal(focusBefore - ResourceAmount.DrawCardFocusCost, updated.Resources.Focus);
    }

    [Fact]
    public void DrawFromDeck_ReturnsDescriptiveMessage()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, message) = svc.DrawFromDeck(game.GameId);
        Assert.Contains("Drew", message);
        Assert.Contains("Focus", message);
    }

    [Fact]
    public void DrawFromDeck_ThrowsWhenNoFocus()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Drain all focus
        game.Resources = game.Resources with { Focus = 0 };
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
    public void PlayCard_LandCard_ConsumesFocus()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var catalog = new CardCatalog();
        var def = catalog.Get(landCard.DefinitionId);
        var focusBefore = game.Resources.Focus;
        var (updated, _) = svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        Assert.Equal(focusBefore - def.FocusCost, updated.Resources.Focus);
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
    public void PlayCard_FailsWhenNotEnoughFocus()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Focus = 0 };
        repo.Save(game);
        var landCard = game.Hand.OfType<LandCard>().First();
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0));
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
        game.Board.GetCell(2, 2).IsLocked = false;
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
        game.Board.GetCell(2, 2).IsLocked = false;
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
        game.Board.GetCell(3, 3).IsLocked = false;
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
        game.Board.GetCell(3, 3).IsLocked = false;
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
        game.Board.GetCell(2, 2).IsLocked = false;
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
        game.Board.GetCell(2, 2).IsLocked = false;
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
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        svc.PlayCard(game.GameId, landCard.InstanceId, 0, 0);
        var game2 = svc.LoadGame(game.GameId);
        var settlement = game2.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        svc.PlayCard(game.GameId, settlement.InstanceId, 0, 0);
        // Invest a second settlement and try to place it in the same spot
        var (afterInvest, _) = svc.Invest(game.GameId, "building_settlement");
        var secondSettlement = afterInvest.Hand.OfType<BuildingCard>()
            .Last(c => c.DefinitionId == "building_settlement");
        Assert.Throws<InvalidOperationException>(() =>
            svc.PlayCard(game.GameId, secondSettlement.InstanceId, 0, 0));
    }

    // ── Invest ──────────────────────────────────────────────────────────────

    [Fact]
    public void Invest_AddsBuildingCardToHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var handSizeBefore = game.Hand.Count;
        var (updated, _) = svc.Invest(game.GameId, "building_settlement");
        Assert.Equal(handSizeBefore + 1, updated.Hand.Count);
        Assert.Contains(updated.Hand, c => c.DefinitionId == "building_settlement");
    }

    [Fact]
    public void Invest_ConsumesResources()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var catalog = new CardCatalog();
        var def = catalog.Get("building_settlement");
        var foodBefore = game.Resources.Food;
        var peopleBefore = game.Resources.People;
        var (updated, _) = svc.Invest(game.GameId, "building_settlement");
        Assert.Equal(foodBefore - def.InvestCost.Food, updated.Resources.Food);
        Assert.Equal(peopleBefore - def.InvestCost.People, updated.Resources.People);
    }

    [Fact]
    public void Invest_ThrowsWhenCantAfford()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        game.Resources = game.Resources with { Food = 0, People = 0 };
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() =>
            svc.Invest(game.GameId, "building_settlement"));
    }

    [Fact]
    public void Invest_AddsLandCardToHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (updated, _) = svc.Invest(game.GameId, "land_plains");
        Assert.Contains(updated.Hand.OfType<LandCard>(), c => c.DefinitionId == "land_plains");
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
    public void EndRound_RestoresFocus()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Spend some focus first
        game.Resources = game.Resources with { Focus = 0 };
        repo.Save(game);
        var (updated, _) = svc.EndRound(game.GameId);
        Assert.Equal(ResourceAmount.FocusPerRound, updated.Resources.Focus);
    }

    [Fact]
    public void EndRound_FocusIsCappedAtMax()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice"); // starts with MaxFocus
        var (updated, _) = svc.EndRound(game.GameId);
        Assert.Equal(ResourceAmount.MaxFocus, updated.Resources.Focus);
    }

    [Fact]
    public void EndRound_SummaryContainsRoundInfo()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var (_, summary) = svc.EndRound(game.GameId);
        Assert.Contains("Round 1", summary);
        Assert.Contains("Focus", summary);
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

    [Fact]
    public void Invest_ThrowsWhenHandFull()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        while (game.Hand.Count < ResourceAmount.MaxHandSize)
            game.Hand.Add(new LandCard { DefinitionId = "land_plains" });
        repo.Save(game);
        Assert.Throws<InvalidOperationException>(() => svc.Invest(game.GameId, "land_plains"));
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
    public void Board_StartsWith6UnlockedCells()
    {
        var board = new StrategyGame.Core.Models.Board.Board();
        var unlocked = board.AllCells().Count(c => !c.IsLocked);
        Assert.Equal(Board.StartRows * Board.StartCols, unlocked);
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

    [Fact]
    public void PlayCard_Land_UnlocksAdjacentCells()
    {
        var repo = new InMemoryGameRepository();
        var svc = new GameService(repo, new CardCatalog());
        var game = svc.StartGame("Alice");
        // Place land at (1,2) — the rightmost column of the starting zone
        var land = game.Hand.OfType<LandCard>().First();
        svc.PlayCard(game.GameId, land.InstanceId, 1, 2);
        var updated = svc.LoadGame(game.GameId);
        // (1,3) should now be unlocked (right neighbor of (1,2))
        Assert.False(updated.Board.GetCell(1, 3).IsLocked);
    }
}

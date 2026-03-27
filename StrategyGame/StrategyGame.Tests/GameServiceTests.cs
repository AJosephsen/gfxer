using StrategyGame.Core.Catalog;
using StrategyGame.Core.Models;
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
}

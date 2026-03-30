using StrategyGame.Core.Catalog;
using StrategyGame.Core.Models.Cards;
using StrategyGame.Core.Services;
using StrategyGame.McpServer.Tools;
using Xunit;

namespace StrategyGame.Tests;

/// <summary>
/// Integration tests that drive the game through MCP tool static methods,
/// validating end-to-end behavior as an MCP client would experience it.
/// </summary>
public sealed class McpToolIntegrationTests
{
    private static GameService CreateService() =>
        new(new InMemoryGameRepository(), new CardCatalog());

    // ── start_game / list_games / load_game ─────────────────────────────────

    [Fact]
    public void StartGame_OutputContainsGameId()
    {
        var svc = CreateService();
        var output = GameManagementTools.StartGame(svc, "Alice");
        Assert.Contains("Game started!", output);
        Assert.Contains("ID:", output);
    }

    [Fact]
    public void StartGame_OutputContainsBoardAndHand()
    {
        var svc = CreateService();
        var output = GameManagementTools.StartGame(svc, "Alice");
        Assert.Contains("Round", output);
        Assert.Contains("Hand", output);
    }

    [Fact]
    public void ListGames_ReturnsNoGamesMessageWhenEmpty()
    {
        var svc = CreateService();
        var output = GameManagementTools.ListGames(svc);
        Assert.Contains("No saved games", output);
    }

    [Fact]
    public void ListGames_ShowsStartedGame()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = GameManagementTools.ListGames(svc);
        Assert.Contains(game.GameId, output);
        Assert.Contains("Alice", output);
    }

    [Fact]
    public void LoadGame_OutputContainsPlayerAndRound()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = GameManagementTools.LoadGame(svc, game.GameId);
        Assert.Contains("Alice", output);
        Assert.Contains("Round 1", output);
    }

    // ── get_board / get_hand ─────────────────────────────────────────────────

    [Fact]
    public void GetBoard_RendersBoard()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = BoardTools.GetBoard(svc, game.GameId);
        Assert.Contains("Round", output);
        Assert.Contains("Board (", output);
    }

    [Fact]
    public void GetHand_RendersFourInitialCards()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = BoardTools.GetHand(svc, game.GameId);
        Assert.Contains("Hand (4/7)", output);
    }

    // ── draw_card ────────────────────────────────────────────────────────────

    [Fact]
    public void DrawCard_OutputMentionsDrewAndFlux()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = DeckTools.DrawCard(svc, game.GameId);
        Assert.Contains("Drew", output);
        Assert.Contains("Flux", output);
    }

    [Fact]
    public void DrawCard_HandSizeIncreases()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        DeckTools.DrawCard(svc, game.GameId);
        var updated = svc.LoadGame(game.GameId);
        Assert.Equal(5, updated.Hand.Count);
    }

    // ── get_market / invest ──────────────────────────────────────────────────

    [Fact]
    public void GetMarket_ListsAvailableCards()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = MarketTools.GetMarket(svc, game.GameId);
        Assert.Contains("Available investments", output);
        Assert.Contains("Your resources", output);
    }

    [Fact]
    public void Invest_AddsCardToHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = MarketTools.Invest(svc, game.GameId, "building_settlement");
        Assert.Contains("Invested", output);
        var updated = svc.LoadGame(game.GameId);
        Assert.Equal(5, updated.Hand.Count);
    }

    // ── play_card ────────────────────────────────────────────────────────────

    [Fact]
    public void PlayCard_Land_OutputContainsBoardAndHand()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var landCard = game.Hand.OfType<LandCard>().First();
        var output = RoundTools.PlayCard(svc, game.GameId, landCard.InstanceId);
        Assert.Contains("Placed", output);
        Assert.Contains("Round", output); // board is rendered
        Assert.Contains("Hand", output);
    }

    [Fact]
    public void PlayCard_InvalidCardId_ReturnsHelpfulMessage()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = RoundTools.PlayCard(svc, game.GameId, "bad-id");
        Assert.Contains("Cannot play card:", output);
        Assert.Contains("not found in hand", output);
    }

    // ── end_round ────────────────────────────────────────────────────────────

    [Fact]
    public void EndRound_OutputContainsSummaryAndBoard()
    {
        var svc = CreateService();
        var game = svc.StartGame("Alice");
        var output = RoundTools.EndRound(svc, game.GameId);
        Assert.Contains("End of Round 1", output);
        Assert.Contains("Flux", output);
        Assert.Contains("Round 2", output); // board is rendered after increment
    }

    // ── Full game flow ───────────────────────────────────────────────────────

    [Fact]
    public void FullFlow_StartPlayLandBuildSettlementEndRound()
    {
        var svc = CreateService();

        // 1. Start game
        var startOutput = GameManagementTools.StartGame(svc, "Bob");
        Assert.Contains("Game started!", startOutput);

        // Extract game ID from the service (simpler than parsing output)
        var game = svc.ListGames().Single();
        var gameId = game.GameId;

        // 2. Draw a card
        var drawOutput = DeckTools.DrawCard(svc, gameId);
        Assert.Contains("Drew", drawOutput);

        // 3. Play a land card on the next open slot
        var state = svc.LoadGame(gameId);
        var landCard = state.Hand.OfType<LandCard>().First();
        var playLandOutput = RoundTools.PlayCard(svc, gameId, landCard.InstanceId);
        Assert.Contains("Placed", playLandOutput);

        // 4. Play the settlement building on the matching land slot
        state = svc.LoadGame(gameId);
        var settlement = state.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        var playBuildingOutput = RoundTools.PlayCard(svc, gameId, settlement.InstanceId);
        Assert.Contains("Built", playBuildingOutput);

        // 5. End round — settlement should produce 2 People
        var endRoundOutput = RoundTools.EndRound(svc, gameId);
        Assert.Contains("End of Round 1", endRoundOutput);
        Assert.Contains("Production", endRoundOutput);

        // 6. Verify final state
        state = svc.LoadGame(gameId);
        Assert.Equal(2, state.Round);
        Assert.NotNull(state.Board.GetCell(0, 0).Building);
        Assert.True(state.Board.GetCell(0, 0).Building!.IsActive);
        // People should be >= 5 + 2 production (settlers produce +2 people)
        Assert.True(state.Resources.People >= 7);
    }

    [Fact]
    public void FullFlow_MultipleRoundsAccumulateResources()
    {
        var svc = CreateService();
        var game = svc.StartGame("Carol");
        var gameId = game.GameId;

        // Place land and settlement on the next available slot
        var landCard = game.Hand.OfType<LandCard>().First();
        svc.PlayCard(gameId, landCard.InstanceId);
        var state = svc.LoadGame(gameId);
        var settlement = state.Hand.OfType<BuildingCard>()
            .First(c => c.DefinitionId == "building_settlement");
        svc.PlayCard(gameId, settlement.InstanceId);

        // End 3 rounds
        for (int i = 0; i < 3; i++)
            svc.EndRound(gameId);

        state = svc.LoadGame(gameId);
        Assert.Equal(4, state.Round);
        // 3 rounds × +2 People from settlement = at least 5 + 6 = 11 (minus any upkeep deltas)
        Assert.True(state.Resources.People >= 5 + 6);
    }
}

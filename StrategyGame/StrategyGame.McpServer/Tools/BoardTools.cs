using System.ComponentModel;
using ModelContextProtocol.Server;
using StrategyGame.Core.Services;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class BoardTools
{
    [McpServerTool(Name = "get_board")]
    [Description("Get the current board state showing all placed land and building cards.")]
    public static string GetBoard(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var game = gameService.LoadGame(gameId);
        return gameService.RenderBoard(game);
    }

    [McpServerTool(Name = "get_hand")]
    [Description("List all cards currently in the player's hand, with their instance IDs, costs, and effects.")]
    public static string GetHand(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var game = gameService.LoadGame(gameId);
        return gameService.RenderHand(game);
    }
}

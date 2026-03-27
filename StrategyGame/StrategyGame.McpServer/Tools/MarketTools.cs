using System.ComponentModel;
using ModelContextProtocol.Server;
using StrategyGame.Core.Services;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class MarketTools
{
    [McpServerTool(Name = "get_market")]
    [Description("Show all cards available for investment and their resource costs. " +
                 "Marks which ones you can currently afford.")]
    public static string GetMarket(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var game = gameService.LoadGame(gameId);
        return gameService.RenderMarket(game);
    }

    [McpServerTool(Name = "invest")]
    [Description("Spend resources to add a new card to your hand. " +
                 "Use get_market to see available card IDs and their costs.")]
    public static string Invest(
        GameService gameService,
        [Description("The game ID.")]
        string gameId,
        [Description("The card definition ID to purchase (e.g. 'land_forest', 'building_farm'). " +
                     "Use get_market to see all valid IDs.")]
        string cardDefinitionId)
    {
        var (game, message) = gameService.Invest(gameId, cardDefinitionId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        return sb.ToString().TrimEnd();
    }
}

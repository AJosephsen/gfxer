using System.ComponentModel;
using ModelContextProtocol.Server;
using StrategyGame.Core.Models;
using StrategyGame.Core.Services;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class DeckTools
{
    [McpServerTool(Name = "draw_card")]
    [Description(
        "Draw a random land card from the map deck and add it to your hand. " +
        $"Costs {ResourceAmount.DrawCardFocusCost} Focus. " +
        "You earn 8 Focus each night (capped at 14 total).")]
    public static string DrawCard(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var (game, message) = gameService.DrawFromDeck(gameId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Focus remaining: {game.Resources.Focus}/{ResourceAmount.MaxFocus}");
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        return sb.ToString().TrimEnd();
    }
}

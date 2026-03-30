using System.ComponentModel;
using ModelContextProtocol.Server;
using StrategyGame.Core.Services;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class RoundTools
{
    [McpServerTool(Name = "play_card")]
    [Description("Play a card from hand onto the board. " +
                 "Land cards are placed on the next open slot automatically. " +
                 "Building cards are attached to the next compatible land slot automatically. " +
                 "Use get_hand to find the card's instance ID.")]
    public static string PlayCard(
        GameService gameService,
        [Description("The game ID.")]
        string gameId,
        [Description("The instance ID of the card in your hand (from get_hand, e.g. 'a3f2b1c0').")]
        string cardInstanceId)
    {
        try
        {
            var (game, message) = gameService.PlayCard(gameId, cardInstanceId);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine();
            sb.AppendLine(gameService.RenderBoard(game));
            sb.AppendLine();
            sb.AppendLine(gameService.RenderHand(game));
            return sb.ToString().TrimEnd();
        }
        catch (InvalidOperationException ex)
        {
            return $"Cannot play card: {ex.Message}";
        }
    }

    [McpServerTool(Name = "end_round")]
    [Description("End the current round. All active buildings on the board produce resources, " +
                 "then upkeep is paid. Buildings that cannot pay upkeep are disabled. " +
                 "Returns a summary of what happened.")]
    public static string EndRound(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var (game, summary) = gameService.EndRound(gameId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine(gameService.RenderBoard(game));
        return sb.ToString().TrimEnd();
    }
}

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
        "Draw the next land card from the map deck and add it to your hand. " +
        "Costs 1 Flux. The deck starts with 500 cards; 15 are burned at the end of each round (~33 rounds max). " +
        "Hand limit is 7 cards — discard first if full.")]
    public static string DrawCard(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var (game, message) = gameService.DrawFromDeck(gameId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine($"Flux remaining: {game.Resources.Flux}/{ResourceAmount.MaxFlux}");
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "discard_card")]
    [Description(
        "Discard a card from your hand to the discard pile. " +
        "Discarded cards are gone permanently. " +
        "Use this to remove unwanted cards or make room in your hand (max 7 cards). " +
        "Use get_hand to find the card's instance ID.")]
    public static string DiscardCard(
        GameService gameService,
        [Description("The game ID.")]
        string gameId,
        [Description("The instance ID of the card to discard (from get_hand, e.g. 'a3f2b1c0').")]
        string cardInstanceId)
    {
        var (game, message) = gameService.DiscardCard(gameId, cardInstanceId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(message);
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        return sb.ToString().TrimEnd();
    }
}

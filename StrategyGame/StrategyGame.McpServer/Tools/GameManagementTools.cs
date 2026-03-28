using System.ComponentModel;
using ModelContextProtocol.Server;
using StrategyGame.Core.Services;

namespace StrategyGame.McpServer.Tools;

[McpServerToolType]
public static class GameManagementTools
{
    [McpServerTool(Name = "start_game")]
    [Description("Start a new strategy game. Returns the game ID, initial board, and starting hand.")]
    public static string StartGame(
        GameService gameService,
        [Description("A name for this game session (e.g. the player's name).")]
        string playerName)
    {
        var game = gameService.StartGame(playerName);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Game started! ID: {game.GameId}");
        sb.AppendLine($"Save this ID — you will need it for all other commands.");
        sb.AppendLine();
        sb.AppendLine(gameService.RenderBoard(game));
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        sb.AppendLine();
        sb.AppendLine("Tip: Play land cards on empty cells, then build on them. End each round to collect resources.");
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "list_games")]
    [Description("List all saved games with their player name, round, and last played time.")]
    public static string ListGames(GameService gameService)
    {
        var games = gameService.ListGames();
        if (games.Count == 0) return "No saved games found. Use start_game to begin.";

        var lines = games.Select(g =>
            $"  {g.GameId}  |  {g.PlayerName}  |  Round {g.Round}  |  {g.LastPlayedAt:yyyy-MM-dd HH:mm} UTC");
        return "Saved games:\n" + string.Join("\n", lines);
    }

    [McpServerTool(Name = "load_game")]
    [Description("Load an existing game by its ID. Returns the current board and hand.")]
    public static string LoadGame(
        GameService gameService,
        [Description("The game ID returned by start_game or list_games.")]
        string gameId)
    {
        var game = gameService.LoadGame(gameId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Game loaded: {gameId}  |  {game.PlayerName}  |  Round {game.Round}");
        sb.AppendLine();
        sb.AppendLine(gameService.RenderBoard(game));
        sb.AppendLine();
        sb.AppendLine(gameService.RenderHand(game));
        return sb.ToString().TrimEnd();
    }

    [McpServerTool(Name = "get_status")]
    [Description("Get a summary of the game status: resources, population (total/occupied/available), and deck cards remaining.")]
    public static string GetStatus(
        GameService gameService,
        [Description("The game ID.")]
        string gameId)
    {
        var game = gameService.LoadGame(gameId);
        var occupied = gameService.GetOccupiedWorkers(game);
        var available = game.Resources.People - occupied;
        var popCap = gameService.GetPopulationCap(game);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== Game Status: {game.PlayerName} — Round {game.Round} ===");
        sb.AppendLine();
        sb.AppendLine($"Resources:  {game.Resources}");
        sb.AppendLine();
        sb.AppendLine($"Population: {game.Resources.People} total (cap: {popCap})");
        sb.AppendLine($"  Occupied: {occupied}");
        sb.AppendLine($"  Available: {available}");
        sb.AppendLine();
        sb.AppendLine($"Land deck:  {game.LandDeck.Count} cards remaining");
        sb.AppendLine($"Hand:       {game.Hand.Count}/{Core.Models.ResourceAmount.MaxHandSize} cards");
        sb.AppendLine($"Discard:    {game.DiscardPile.Count} cards");
        return sb.ToString().TrimEnd();
    }
}

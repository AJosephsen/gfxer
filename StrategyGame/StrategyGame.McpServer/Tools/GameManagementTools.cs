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
}

using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyGame.Core.Models.Game;

namespace StrategyGame.Core.Services;

public sealed record GameSummary(
    string GameId,
    string PlayerName,
    int Round,
    DateTimeOffset LastPlayedAt);

/// <summary>
/// Persists game state as JSON files in a local directory.
/// One file per game: {savesDir}/{gameId}.json
/// </summary>
public sealed class GameRepository : IGameRepository
{
    private readonly string _savesDir;

    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public GameRepository()
    {
        _savesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StrategyGame", "saves");
        Directory.CreateDirectory(_savesDir);
    }

    public void Save(GameState game)
    {
        var json = JsonSerializer.Serialize(game, SaveOptions);
        File.WriteAllText(GetPath(game.GameId), json);
    }

    public GameState Load(string gameId)
    {
        var path = GetPath(gameId);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Game '{gameId}' not found.");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GameState>(json, SaveOptions)
            ?? throw new InvalidOperationException($"Failed to load game '{gameId}'.");
    }

    public List<GameSummary> ListAll()
    {
        return Directory.GetFiles(_savesDir, "*.json")
            .Select(path =>
            {
                var game = Load(Path.GetFileNameWithoutExtension(path));
                return new GameSummary(game.GameId, game.PlayerName, game.Round, game.LastPlayedAt);
            })
            .OrderByDescending(s => s.LastPlayedAt)
            .ToList();
    }

    public void Delete(string gameId)
    {
        var path = GetPath(gameId);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Game '{gameId}' not found.");
        File.Delete(path);
    }

    private string GetPath(string gameId) => Path.Combine(_savesDir, $"{gameId}.json");
}

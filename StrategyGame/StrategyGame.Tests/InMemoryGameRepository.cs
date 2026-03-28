using StrategyGame.Core.Models.Game;
using StrategyGame.Core.Services;

namespace StrategyGame.Tests;

/// <summary>
/// Thread-safe in-memory game repository for use in tests.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly Dictionary<string, GameState> _store = new();

    public void Save(GameState game) =>
        _store[game.GameId] = game;

    public GameState Load(string gameId)
    {
        if (!_store.TryGetValue(gameId, out var game))
            throw new InvalidOperationException($"Game '{gameId}' not found.");
        return game;
    }

    public void Delete(string gameId)
    {
        if (!_store.Remove(gameId))
            throw new InvalidOperationException($"Game '{gameId}' not found.");
    }

    public List<GameSummary> ListAll() =>
        _store.Values
              .Select(g => new GameSummary(g.GameId, g.PlayerName, g.Round, g.LastPlayedAt))
              .OrderByDescending(s => s.LastPlayedAt)
              .ToList();
}

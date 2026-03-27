using StrategyGame.Core.Models.Cards;
using BoardModel = StrategyGame.Core.Models.Board.Board;

namespace StrategyGame.Core.Models.Game;

public sealed class GameState
{
    public string GameId { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public required string PlayerName { get; set; }
    public int Round { get; set; } = 1;

    public BoardModel Board { get; set; } = new();

    /// <summary>Cards currently in the player's hand (mix of land and building cards).</summary>
    public List<CardBase> Hand { get; set; } = [];

    /// <summary>Current player resources.</summary>
    public ResourceAmount Resources { get; set; } = ResourceAmount.Zero;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastPlayedAt { get; set; } = DateTimeOffset.UtcNow;
}

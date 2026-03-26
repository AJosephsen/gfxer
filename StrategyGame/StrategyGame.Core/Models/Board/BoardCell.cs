using StrategyGame.Core.Models.Cards;

namespace StrategyGame.Core.Models.Board;

public sealed class BoardCell
{
    public int Row { get; init; }
    public int Col { get; init; }

    /// <summary>The terrain card on this cell, if any.</summary>
    public LandCard? Land { get; set; }

    /// <summary>The building on this cell, if any. Requires Land to be set first.</summary>
    public BuildingCard? Building { get; set; }

    public bool IsEmpty => Land == null;
    public bool CanBuild => Land != null && Building == null;
}

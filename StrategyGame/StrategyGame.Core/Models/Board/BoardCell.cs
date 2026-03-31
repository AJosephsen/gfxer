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

    /// <summary>
    /// Locked cells cannot receive cards until a land card is placed on an orthogonally adjacent cell.
    /// The starting board has only a small central region unlocked.
    /// </summary>
    public bool IsLocked { get; set; }

    public bool IsEmpty => Land != null && Land.DefinitionId == "land_empty";
    public bool HasTerrain => Land != null && Land.DefinitionId != "land_empty";
    public bool CanBuild => HasTerrain && Building == null;
}

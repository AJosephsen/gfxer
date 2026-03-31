namespace StrategyGame.Core.Models.Cards;

/// <summary>
/// A building card placed on top of a land card. Produces or consumes resources each round.
/// </summary>
public sealed class BuildingCard : CardBase
{
    /// <summary>
    /// False if the building was disabled due to unpaid upkeep.
    /// A disabled building neither produces nor consumes resources.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of workers currently assigned to this building.
    /// Set during EndRound by round-robin distribution.
    /// Production scales linearly: output = base × (AssignedWorkers / Occupies).
    /// </summary>
    public int AssignedWorkers { get; set; } = 0;

    /// <summary>
    /// Maximum population this settlement can support (10–40, rolled at creation).
    /// Only meaningful for Settlement buildings; 0 for non-settlements.
    /// Total population is capped at the sum of all settlement PopulationCapacity on the board.
    /// </summary>
    public int PopulationCapacity { get; init; } = 0;

    /// <summary>Create a new building card, rolling stats where applicable.</summary>
    public static BuildingCard Create(string definitionId, int level = 1) => new()
    {
        DefinitionId = definitionId,
        Level = level,
        PopulationCapacity = definitionId == "building_settlement"
            ? 10 + Random.Shared.Next(31)  // 10–40
            : 0,
    };
}

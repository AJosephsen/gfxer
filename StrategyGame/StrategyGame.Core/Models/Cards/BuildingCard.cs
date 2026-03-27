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
}

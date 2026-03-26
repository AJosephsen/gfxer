using System.Text.Json.Serialization;

namespace StrategyGame.Core.Models.Cards;

/// <summary>
/// A card instance held in a player's hand or placed on the board.
/// Stores only per-instance state; all rules are resolved via DefinitionId → CardCatalog.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LandCard), "land")]
[JsonDerivedType(typeof(BuildingCard), "building")]
public abstract class CardBase
{
    /// <summary>Unique identifier for this instance (not the definition).</summary>
    public string InstanceId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Foreign key into CardCatalog (e.g. "land_forest", "building_farm").</summary>
    public required string DefinitionId { get; init; }
}

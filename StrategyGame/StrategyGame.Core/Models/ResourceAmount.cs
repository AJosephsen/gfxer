namespace StrategyGame.Core.Models;

/// <summary>
/// Immutable value object representing an amount of each resource.
/// Used for current player resources, card costs, production, and upkeep.
/// </summary>
public record ResourceAmount(int Food = 0, int People = 0)
{
    public static readonly ResourceAmount Zero = new();

    public ResourceAmount Add(ResourceAmount other) =>
        new(Food + other.Food, People + other.People);

    public ResourceAmount Subtract(ResourceAmount other) =>
        new(Food - other.Food, People - other.People);

    public bool CanAfford(ResourceAmount cost) =>
        Food >= cost.Food && People >= cost.People;

    public bool IsEmpty => Food == 0 && People == 0;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Food != 0) parts.Add($"{Food} Food");
        if (People != 0) parts.Add($"{People} People");
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}

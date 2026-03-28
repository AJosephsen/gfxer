namespace StrategyGame.Core.Models;

/// <summary>
/// Immutable value object representing an amount of each resource.
/// Used for current player resources, card costs, production, and upkeep.
/// Focus is the action resource: earned each night, spent to draw and play cards.
/// </summary>
public record ResourceAmount(int Food = 0, int People = 0, int Wood = 0, int Focus = 0)
{
    public static readonly ResourceAmount Zero = new();

    /// <summary>Maximum Focus a player can hold at once.</summary>
    public const int MaxFocus = 14;

    /// <summary>Focus earned at the end of each round.</summary>
    public const int FocusPerRound = 8;

    /// <summary>Focus cost to draw a random land card from the deck.</summary>
    public const int DrawCardFocusCost = 1;

    public ResourceAmount Add(ResourceAmount other) =>
        new(Food + other.Food, People + other.People, Wood + other.Wood, Focus + other.Focus);

    public ResourceAmount Subtract(ResourceAmount other) =>
        new(Food - other.Food, People - other.People, Wood - other.Wood, Focus - other.Focus);

    /// <summary>Add Focus but clamp to MaxFocus.</summary>
    public ResourceAmount AddFocus(int amount) =>
        this with { Focus = Math.Min(Focus + amount, MaxFocus) };

    public bool CanAfford(ResourceAmount cost) =>
        Food >= cost.Food && People >= cost.People && Wood >= cost.Wood && Focus >= cost.Focus;

    public bool IsEmpty => Food == 0 && People == 0 && Wood == 0 && Focus == 0;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Food != 0) parts.Add($"{Food} Food");
        if (People != 0) parts.Add($"{People} People");
        if (Wood != 0) parts.Add($"{Wood} Wood");
        if (Focus != 0) parts.Add($"{Focus} Focus");
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}

namespace StrategyGame.Core.Models;

/// <summary>
/// Immutable value object representing an amount of each resource.
/// Used for current player resources, card costs, production, and upkeep.
/// Flux is the action resource: earned each night, spent to draw and play cards.
/// </summary>
public record ResourceAmount(int Food = 0, int People = 0, int Wood = 0, int Flux = 0)
{
    public static readonly ResourceAmount Zero = new();

    /// <summary>Maximum Flux a player can hold at once.</summary>
    public const int MaxFlux = 14;

    /// <summary>Flux earned at the end of each round.</summary>
    public const int FluxPerRound = 8;

    /// <summary>Flux cost to draw a random land card from the deck.</summary>
    public const int DrawCardFluxCost = 1;

    /// <summary>Maximum number of cards a player can hold in hand at once.</summary>
    public const int MaxHandSize = 7;

    /// <summary>Number of land cards in the deck at the start of a game.</summary>
    public const int LandDeckSize = 500;

    /// <summary>Cards burned (removed) from the land deck at the end of each round. Max rounds ≈ 500 / 15 = 33.</summary>
    public const int DeckBurnPerRound = 15;

    public ResourceAmount Add(ResourceAmount other) =>
        new(Food + other.Food, People + other.People, Wood + other.Wood, Flux + other.Flux);

    public ResourceAmount Subtract(ResourceAmount other) =>
        new(Food - other.Food, People - other.People, Wood - other.Wood, Flux - other.Flux);

    /// <summary>Add Flux but clamp to MaxFlux.</summary>
    public ResourceAmount AddFlux(int amount) =>
        this with { Flux = Math.Min(Flux + amount, MaxFlux) };

    public bool CanAfford(ResourceAmount cost) =>
        Food >= cost.Food && People >= cost.People && Wood >= cost.Wood && Flux >= cost.Flux;

    public bool IsEmpty => Food == 0 && People == 0 && Wood == 0 && Flux == 0;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Food != 0) parts.Add($"{Food} Food");
        if (People != 0) parts.Add($"{People} People");
        if (Wood != 0) parts.Add($"{Wood} Wood");
        if (Flux != 0) parts.Add($"{Flux} Flux");
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }
}

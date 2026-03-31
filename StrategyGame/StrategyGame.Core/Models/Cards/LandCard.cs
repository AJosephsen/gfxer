namespace StrategyGame.Core.Models.Cards;

/// <summary>
/// A terrain card placed on an empty board cell to enable building.
/// Each instance has rolled stats giving it unique quality and access cost.
/// </summary>
public sealed class LandCard : CardBase
{
    /// <summary>
    /// Fertility of this tile, in tenths (range 5–15).
    /// 10 = 1.0× (average). Scales production of buildings placed here (future mechanic).
    /// Roll: 5 + d(0–5) + d(0–5).
    /// </summary>
    public int Fertility { get; init; } = 10;

    /// <summary>
    /// Terrain accessibility, in tenths (range 5–12).
    /// 10 = 1.0× (average). Scales the Flux cost to play this card from hand.
    /// Roll: 5 + d(0–4) + d(0–3).
    /// </summary>
    public int AccessibilityCost { get; init; } = 10;

    /// <summary>Create a new land card with randomly rolled stats.</summary>
    public static LandCard Create(string definitionId, int level = 1) => new()
    {
        DefinitionId = definitionId,
        Level = level,
        Fertility        = 5 + Random.Shared.Next(6) + Random.Shared.Next(6), // 5–15
        AccessibilityCost = 5 + Random.Shared.Next(5) + Random.Shared.Next(4), // 5–12
    };

    /// <summary>Flux cost to play this card, derived from catalog base × AccessibilityCost.</summary>
    public int ComputeFluxCost(int baseFluxCost) =>
        Math.Max(1, (int)Math.Round(baseFluxCost * AccessibilityCost / 10.0,
            MidpointRounding.AwayFromZero));
}

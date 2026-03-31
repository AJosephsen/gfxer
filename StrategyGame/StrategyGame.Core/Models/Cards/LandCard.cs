using StrategyGame.Core.Catalog;

namespace StrategyGame.Core.Models.Cards;

/// <summary>
/// A terrain card placed on an empty board cell to enable building.
/// Each instance has rolled Fertility and a pre-computed FluxCost.
/// </summary>
public sealed class LandCard : CardBase
{
    /// <summary>
    /// Fertility of this tile, in tenths.
    /// 10 = 1.0× (average). Scales production of buildings placed here.
    /// </summary>
    public int Fertility { get; init; } = 10;

    /// <summary>
    /// Pre-computed Flux cost to play this card from hand.
    /// Rolled at creation from base cost × fluxScale range.
    /// </summary>
    public int FluxCost { get; init; } = 0;

    /// <summary>Create a new land card with randomly rolled stats from the catalog definition.</summary>
    public static LandCard Create(LandDefinition def) => Create(def, Random.Shared);

    /// <summary>Create a new land card with rolled stats, using a specific Random for testability.</summary>
    public static LandCard Create(LandDefinition def, Random rng)
    {
        var fr = def.StatRanges.Fertility;
        var fertility = fr.Min + rng.Next(fr.Max - fr.Min + 1);

        var sr = def.StatRanges.FluxScale;
        var fluxScale = sr.Min + rng.Next(sr.Max - sr.Min + 1);
        var fluxCost = Math.Max(1, (int)Math.Round(def.FluxCost * fluxScale / 10.0,
            MidpointRounding.AwayFromZero));

        return new()
        {
            DefinitionId = def.Id,
            Level = def.Level,
            Fertility = fertility,
            FluxCost = fluxCost,
        };
    }

    /// <summary>Create an Empty land card (placeholder for unclaimed board slots).</summary>
    public static LandCard CreateEmpty() => new()
    {
        DefinitionId = "land_empty",
        Level = 1,
        Fertility = 0,
        FluxCost = 0,
    };
}

using System.Text.Json.Serialization;
using StrategyGame.Core.Models;

namespace StrategyGame.Core.Catalog;

public enum TerrainType { Empty, Forest, Plains, Hill, Beach, Wasteland }
public enum BuildingType { Settlement, Farm, LumberCamp, FishingCamp, SheepPasture }

/// <summary>
/// Static blueprint for a card type loaded from card-catalog.json.
/// Never mutated at runtime; always resolved by ID from CardCatalog.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LandDefinition), "land")]
[JsonDerivedType(typeof(BuildingDefinition), "building")]
public abstract record CardDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public int Level { get; init; } = 1;
    public string[] Tags { get; init; } = [];
    public string? InheritsFrom { get; init; }
    public bool EnabledInGame { get; init; } = true;
    public PlacementRequirements PlacementRequirements { get; init; } = new();

    /// <summary>Resources required to acquire this card from the market.</summary>
    public ResourceAmount InvestCost { get; init; } = ResourceAmount.Zero;

    /// <summary>
    /// Flux spent when playing this card from hand onto the board.
    /// Default is 3 for all card types.
    /// </summary>
    public int FluxCost { get; init; } = 3;
}

public sealed record LandDefinition : CardDefinition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TerrainType Terrain { get; init; }

    /// <summary>Configurable stat roll ranges for land card instances.</summary>
    public LandStatRanges StatRanges { get; init; } = new();
}

/// <summary>Min/max range for a single rolled stat (in tenths, 10 = ×1.0).</summary>
public sealed record StatRange
{
    public int Min { get; init; } = 10;
    public int Max { get; init; } = 10;
}

/// <summary>Configurable ranges for rolled stats on land card instances.</summary>
public sealed record LandStatRanges
{
    /// <summary>Fertility range (in tenths). Default: fixed at 10 (×1.0).</summary>
    public StatRange Fertility { get; init; } = new();

    /// <summary>Flux cost scale range (in tenths). Rolled at creation, multiplied against base FluxCost. Default: fixed at 10 (×1.0).</summary>
    public StatRange FluxScale { get; init; } = new();
}

public sealed record BuildingDefinition : CardDefinition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required BuildingType BuildingType { get; init; }

    /// <summary>Allowed terrain types. Empty array means any terrain is valid.</summary>
    [JsonConverter(typeof(TerrainArrayConverter))]
    public TerrainType[] AllowedTerrains { get; init; } = [];

    /// <summary>Number of workers (people) this building occupies while active.</summary>
    public int Occupies { get; init; } = 0;

    /// <summary>Resources spent when playing this card from hand onto the board.</summary>
    public ResourceAmount PlayCost { get; init; } = ResourceAmount.Zero;

    /// <summary>Resources consumed each round (paid after production).</summary>
    public ResourceAmount Upkeep { get; init; } = ResourceAmount.Zero;

    /// <summary>Resources generated each round.</summary>
    public ResourceAmount Production { get; init; } = ResourceAmount.Zero;

    public bool CanBuildOn(TerrainType terrain) =>
        terrain != TerrainType.Wasteland &&
        terrain != TerrainType.Empty &&
        (AllowedTerrains.Length == 0 || AllowedTerrains.Contains(terrain));
}

public sealed record PlacementRequirements
{
    public string[] CellTags { get; init; } = [];
    public string[] CellTagsAll { get; init; } = [];
    public TargetCardRequirement? TargetCard { get; init; }
    public Dictionary<string, int> Technology { get; init; } = [];
}

public sealed record TargetCardRequirement
{
    public string? DefinitionId { get; init; }
    public int MinLevel { get; init; } = 1;
}

/// <summary>Converts a JSON string array of terrain names to TerrainType[].</summary>
public sealed class TerrainArrayConverter : System.Text.Json.Serialization.JsonConverter<TerrainType[]>
{
    public override TerrainType[] Read(ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        var strings = System.Text.Json.JsonSerializer.Deserialize<string[]>(ref reader, options);
        if (strings == null || strings.Length == 0) return [];
        return strings.Select(s => Enum.Parse<TerrainType>(s, ignoreCase: true)).ToArray();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer,
        TerrainType[] value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var t in value) writer.WriteStringValue(t.ToString());
        writer.WriteEndArray();
    }
}

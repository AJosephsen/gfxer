using System.Text.Json.Serialization;
using StrategyGame.Core.Models;

namespace StrategyGame.Core.Catalog;

public enum TerrainType { Forest, Plains, Hill, Beach }
public enum BuildingType { Settlement, Farm }

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

    /// <summary>Resources required to acquire this card from the market.</summary>
    public ResourceAmount InvestCost { get; init; } = ResourceAmount.Zero;
}

public sealed record LandDefinition : CardDefinition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required TerrainType Terrain { get; init; }
}

public sealed record BuildingDefinition : CardDefinition
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required BuildingType BuildingType { get; init; }

    /// <summary>Allowed terrain types. Empty array means any terrain is valid.</summary>
    [JsonConverter(typeof(TerrainArrayConverter))]
    public TerrainType[] AllowedTerrains { get; init; } = [];

    /// <summary>Resources spent when playing this card from hand onto the board.</summary>
    public ResourceAmount PlayCost { get; init; } = ResourceAmount.Zero;

    /// <summary>Resources consumed each round (paid after production).</summary>
    public ResourceAmount Upkeep { get; init; } = ResourceAmount.Zero;

    /// <summary>Resources generated each round.</summary>
    public ResourceAmount Production { get; init; } = ResourceAmount.Zero;

    public bool CanBuildOn(TerrainType terrain) =>
        AllowedTerrains.Length == 0 || AllowedTerrains.Contains(terrain);
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

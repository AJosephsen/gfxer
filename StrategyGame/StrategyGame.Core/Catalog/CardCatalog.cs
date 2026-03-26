using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StrategyGame.Core.Catalog;

/// <summary>
/// Loads card definitions from the embedded card-catalog.json and provides fast lookup by ID.
/// Registered as a singleton; safe for concurrent reads.
/// </summary>
public sealed class CardCatalog
{
    private readonly Dictionary<string, CardDefinition> _byId;

    private static readonly JsonSerializerOptions CatalogOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public CardCatalog()
    {
        _byId = Load();
    }

    public CardDefinition Get(string id) =>
        _byId.TryGetValue(id, out var def) ? def
        : throw new KeyNotFoundException($"Card definition '{id}' not found in catalog.");

    public IEnumerable<CardDefinition> All => _byId.Values;

    public IEnumerable<LandDefinition> LandCards =>
        _byId.Values.OfType<LandDefinition>();

    public IEnumerable<BuildingDefinition> BuildingCards =>
        _byId.Values.OfType<BuildingDefinition>();

    private static Dictionary<string, CardDefinition> Load()
    {
        var resourceName = "StrategyGame.Core.Data.card-catalog.json";
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var doc = JsonSerializer.Deserialize<CatalogDocument>(stream, CatalogOptions)
            ?? throw new InvalidOperationException("Failed to deserialize card catalog.");

        return doc.Cards.ToDictionary(c => c.Id);
    }

    private sealed record CatalogDocument(List<CardDefinition> Cards);
}

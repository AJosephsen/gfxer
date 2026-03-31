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

    public IEnumerable<CardDefinition> AllPlayable =>
        _byId.Values.Where(d => d.EnabledInGame);

    public IEnumerable<LandDefinition> LandCards =>
        _byId.Values.OfType<LandDefinition>().Where(d => d.EnabledInGame);

    public IEnumerable<BuildingDefinition> BuildingCards =>
        _byId.Values.OfType<BuildingDefinition>().Where(d => d.EnabledInGame);

    private static Dictionary<string, CardDefinition> Load()
    {
        var resourceName = "StrategyGame.Core.Data.card-catalog.json";
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var doc = JsonSerializer.Deserialize<CatalogDocument>(stream, CatalogOptions)
            ?? throw new InvalidOperationException("Failed to deserialize card catalog.");

        var rawById = doc.Cards.ToDictionary(c => c.Id);
        var resolved = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        var resolving = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in rawById.Keys)
            Resolve(id, rawById, resolved, resolving);

        return resolved;
    }

    private static CardDefinition Resolve(
        string id,
        Dictionary<string, CardDefinition> rawById,
        Dictionary<string, CardDefinition> resolved,
        HashSet<string> resolving)
    {
        if (resolved.TryGetValue(id, out var existing))
            return existing;

        if (!rawById.TryGetValue(id, out var raw))
            throw new KeyNotFoundException($"Card definition '{id}' not found in catalog.");

        if (!resolving.Add(id))
            throw new InvalidOperationException($"Circular inheritance detected while resolving '{id}'.");

        CardDefinition merged;
        if (string.IsNullOrWhiteSpace(raw.InheritsFrom))
        {
            merged = raw;
        }
        else
        {
            var parent = Resolve(raw.InheritsFrom, rawById, resolved, resolving);
            merged = Merge(parent, raw);
        }

        resolving.Remove(id);
        resolved[id] = merged;
        return merged;
    }

    private static CardDefinition Merge(CardDefinition parent, CardDefinition child)
    {
        if (parent.GetType() != child.GetType())
            throw new InvalidOperationException(
                $"Card '{child.Id}' inherits from '{parent.Id}' but changes type from {parent.GetType().Name} to {child.GetType().Name}.");

        return (parent, child) switch
        {
            (LandDefinition parentLand, LandDefinition childLand) => MergeLand(parentLand, childLand),
            (BuildingDefinition parentBuilding, BuildingDefinition childBuilding) => MergeBuilding(parentBuilding, childBuilding),
            _ => throw new InvalidOperationException($"Unsupported card type for inheritance merge: {child.GetType().Name}.")
        };
    }

    private static LandDefinition MergeLand(LandDefinition parent, LandDefinition child) =>
        child with
        {
            Tags = MergeTags(parent.Tags, child.Tags),
            PlacementRequirements = MergePlacementRequirements(parent.PlacementRequirements, child.PlacementRequirements)
        };

    private static BuildingDefinition MergeBuilding(BuildingDefinition parent, BuildingDefinition child) =>
        child with
        {
            Tags = MergeTags(parent.Tags, child.Tags),
            PlacementRequirements = MergePlacementRequirements(parent.PlacementRequirements, child.PlacementRequirements),
            AllowedTerrains = MergeAllowedTerrains(parent.AllowedTerrains, child.AllowedTerrains)
        };

    private static string[] MergeTags(string[] parent, string[] child) =>
        parent.Concat(child)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static TerrainType[] MergeAllowedTerrains(TerrainType[] parent, TerrainType[] child) =>
        child.Length == 0 ? parent : child;

    private static PlacementRequirements MergePlacementRequirements(
        PlacementRequirements parent,
        PlacementRequirements child)
    {
        var technology = new Dictionary<string, int>(parent.Technology, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in child.Technology)
            technology[kvp.Key] = kvp.Value;

        return child with
        {
            CellTags = parent.CellTags
                .Concat(child.CellTags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CellTagsAll = parent.CellTagsAll
                .Concat(child.CellTagsAll)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TargetCard = child.TargetCard ?? parent.TargetCard,
            Technology = technology
        };
    }

    private sealed record CatalogDocument(List<CardDefinition> Cards);
}

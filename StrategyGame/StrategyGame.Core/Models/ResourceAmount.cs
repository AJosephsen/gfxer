using System.Text.Json;
using System.Text.Json.Serialization;

namespace StrategyGame.Core.Models;

/// <summary>
/// Immutable-ish value object representing an amount of each resource.
/// Supports the legacy fixed resources (Food/People/Wood/Flux) plus arbitrary extra resource IDs.
/// </summary>
[JsonConverter(typeof(ResourceAmountJsonConverter))]
public sealed record ResourceAmount
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

    public int Food { get; init; }
    public int People { get; init; }
    public int Wood { get; init; }
    public int Flux { get; init; }

    public Dictionary<string, int> ExtraResources { get; init; }

    public ResourceAmount(
        int Food = 0,
        int People = 0,
        int Wood = 0,
        int Flux = 0,
        Dictionary<string, int>? ExtraResources = null)
    {
        this.Food = Food;
        this.People = People;
        this.Wood = Wood;
        this.Flux = Flux;
        this.ExtraResources = ExtraResources is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(ExtraResources, StringComparer.OrdinalIgnoreCase);
    }

    public int Get(string resourceId) => Normalize(resourceId) switch
    {
        "food" => Food,
        "people" => People,
        "wood" => Wood,
        "flux" => Flux,
        var key => ExtraResources.TryGetValue(key, out var value) ? value : 0
    };

    public ResourceAmount Set(string resourceId, int amount)
    {
        var key = Normalize(resourceId);
        return key switch
        {
            "food" => this with { Food = amount },
            "people" => this with { People = amount },
            "wood" => this with { Wood = amount },
            "flux" => this with { Flux = amount },
            _ => this with
            {
                ExtraResources = new Dictionary<string, int>(ExtraResources, StringComparer.OrdinalIgnoreCase)
                {
                    [key] = amount
                }
            }
        };
    }

    public IReadOnlyDictionary<string, int> ToDictionary(bool includeZeroKnown = true)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (includeZeroKnown || Food != 0) result["food"] = Food;
        if (includeZeroKnown || People != 0) result["people"] = People;
        if (includeZeroKnown || Wood != 0) result["wood"] = Wood;
        if (includeZeroKnown || Flux != 0) result["flux"] = Flux;

        foreach (var kvp in ExtraResources.Where(kvp => includeZeroKnown || kvp.Value != 0))
            result[kvp.Key] = kvp.Value;

        return result;
    }

    public ResourceAmount Add(ResourceAmount other)
    {
        var merged = new Dictionary<string, int>(ExtraResources, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in other.ExtraResources)
            merged[kvp.Key] = merged.TryGetValue(kvp.Key, out var current) ? current + kvp.Value : kvp.Value;

        return new ResourceAmount(
            Food: Food + other.Food,
            People: People + other.People,
            Wood: Wood + other.Wood,
            Flux: Flux + other.Flux,
            ExtraResources: merged);
    }

    public ResourceAmount Subtract(ResourceAmount other)
    {
        var merged = new Dictionary<string, int>(ExtraResources, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in other.ExtraResources)
            merged[kvp.Key] = merged.TryGetValue(kvp.Key, out var current) ? current - kvp.Value : -kvp.Value;

        return new ResourceAmount(
            Food: Food - other.Food,
            People: People - other.People,
            Wood: Wood - other.Wood,
            Flux: Flux - other.Flux,
            ExtraResources: merged);
    }

    public ResourceAmount Scale(double ratio)
    {
        var scaledExtras = ExtraResources.ToDictionary(
            kvp => kvp.Key,
            kvp => (int)Math.Round(kvp.Value * ratio),
            StringComparer.OrdinalIgnoreCase);

        return new ResourceAmount(
            Food: (int)Math.Round(Food * ratio),
            People: (int)Math.Round(People * ratio),
            Wood: (int)Math.Round(Wood * ratio),
            Flux: (int)Math.Round(Flux * ratio),
            ExtraResources: scaledExtras);
    }

    /// <summary>Add Flux but clamp to MaxFlux.</summary>
    public ResourceAmount AddFlux(int amount) =>
        this with { Flux = Math.Min(Flux + amount, MaxFlux) };

    public bool CanAfford(ResourceAmount cost)
    {
        if (Food < cost.Food || People < cost.People || Wood < cost.Wood || Flux < cost.Flux)
            return false;

        foreach (var kvp in cost.ExtraResources)
        {
            if (Get(kvp.Key) < kvp.Value)
                return false;
        }

        return true;
    }

    public bool IsEmpty => Food == 0 && People == 0 && Wood == 0 && Flux == 0 && ExtraResources.Values.All(v => v == 0);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Food != 0) parts.Add($"{Food} Food");
        if (People != 0) parts.Add($"{People} People");
        if (Wood != 0) parts.Add($"{Wood} Wood");
        if (Flux != 0) parts.Add($"{Flux} Flux");

        foreach (var kvp in ExtraResources.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (kvp.Value != 0)
                parts.Add($"{kvp.Value} {DisplayName(kvp.Key)}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    private static string Normalize(string resourceId) => resourceId.Trim().ToLowerInvariant();

    private static string DisplayName(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return resourceId;
        var value = resourceId.Replace('-', ' ').Replace('_', ' ');
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}

public sealed class ResourceAmountJsonConverter : JsonConverter<ResourceAmount>
{
    public override ResourceAmount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("ResourceAmount must be a JSON object.");

        int food = 0;
        int people = 0;
        int wood = 0;
        int flux = 0;
        var extra = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new ResourceAmount(food, people, wood, flux, extra);

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Unexpected token in ResourceAmount JSON.");

            var propertyName = reader.GetString() ?? throw new JsonException("Resource property name was null.");
            reader.Read();
            var value = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : 0;

            switch (propertyName.ToLowerInvariant())
            {
                case "food":
                    food = value;
                    break;
                case "people":
                    people = value;
                    break;
                case "wood":
                    wood = value;
                    break;
                case "flux":
                    flux = value;
                    break;
                default:
                    extra[propertyName] = value;
                    break;
            }
        }

        throw new JsonException("Unexpected end of ResourceAmount JSON.");
    }

    public override void Write(Utf8JsonWriter writer, ResourceAmount value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("food", value.Food);
        writer.WriteNumber("people", value.People);
        writer.WriteNumber("wood", value.Wood);
        writer.WriteNumber("flux", value.Flux);

        foreach (var kvp in value.ExtraResources.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            writer.WriteNumber(kvp.Key, kvp.Value);

        writer.WriteEndObject();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// One market pair from a currency-exchange digest. The metric dictionaries are keyed by item id,
/// e.g. <c>{"chaos_orb": 12345}</c>.
/// </summary>
public sealed record MarketDigest(
    [property: JsonPropertyName("league")] string League,
    [property: JsonPropertyName("market_id")] string MarketId,
    [property: JsonPropertyName("market_pair"), JsonConverter(typeof(MarketPairJsonConverter))] MarketPair MarketPair,
    [property: JsonPropertyName("volume_traded")] Dictionary<string, long> VolumeTraded,
    [property: JsonPropertyName("lowest_stock")] Dictionary<string, long> LowestStock,
    [property: JsonPropertyName("highest_stock")] Dictionary<string, long> HighestStock,
    [property: JsonPropertyName("lowest_ratio")] Dictionary<string, double> LowestRatio,
    [property: JsonPropertyName("highest_ratio")] Dictionary<string, double> HighestRatio);

/// <summary>The two items of a market pair, in the order returned by the API.</summary>
public sealed record MarketPair(string ItemA, string ItemB)
{
    /// <summary>First item id (matches <see cref="MarketDigest.MarketId"/> prefix).</summary>
    public string CurrencyA => ItemA;

    /// <summary>Second item id (matches <see cref="MarketDigest.MarketId"/> suffix).</summary>
    public string CurrencyB => ItemB;
}

/// <summary>
/// Binds the API's <c>market_pair</c> element — a two-item JSON array ordered as in
/// <see cref="MarketDigest.MarketId"/> (e.g. <c>["item_a", "item_b"]</c>) — to <see cref="MarketPair"/>.
/// System.Text.Json cannot bind an array to a tuple or a positional record, so the API's array shape
/// needs an explicit converter.
/// </summary>
public sealed class MarketPairJsonConverter : JsonConverter<MarketPair>
{
    public override MarketPair? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected market_pair to be a two-element JSON array.");

        var items = new List<string>(2);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } item)
                throw new JsonException("market_pair elements must be non-null strings.");

            items.Add(item);
            if (items.Count > 2)
                throw new JsonException("market_pair must contain exactly two elements.");
        }

        if (items.Count != 2)
            throw new JsonException($"market_pair must contain exactly two elements, but {items.Count} were read.");

        return new MarketPair(items[0], items[1]);
    }

    public override void Write(Utf8JsonWriter writer, MarketPair value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.ItemA);
        writer.WriteStringValue(value.ItemB);
        writer.WriteEndArray();
    }
}

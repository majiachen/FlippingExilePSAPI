using System.Text.Json.Serialization;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Payload of the PoE trade data endpoint <c>GET /api/trade/data/static</c>: the complete item
/// catalogue of the current game version, grouped by category.
/// <para>
/// The catalogue carries no metadata path field. It is embedded in each entry's <c>image</c> URL as
/// a base64url-encoded JSON array — see <see cref="TradeStaticMetadataDecoder"/>.
/// </para>
/// </summary>
public sealed record TradeStaticResponse(
    [property: JsonPropertyName("result")] IReadOnlyList<TradeStaticCategory>? Result);

/// <summary>One catalogue category (e.g. <c>Currency</c>, <c>Maps</c>).</summary>
public sealed record TradeStaticCategory(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("entries")] IReadOnlyList<TradeStaticEntry>? Entries);

/// <summary>
/// One catalogue entry. Example:
/// <c>{"id":"chaos","text":"Chaos Orb","image":"/gen/image/WzI1…/6308fc8ca2/CurrencyRerollRare.png"}</c>.
/// </summary>
/// <param name="Id">Trade site item alias (e.g. <c>chaos</c>); accepted as a lookup term.</param>
/// <param name="Text">Item display name — the value a stash item's <c>name</c>/<c>base_type</c> is compared against.</param>
/// <param name="Image">Icon URL containing the metadata path. Absent for entries with no icon.</param>
public sealed record TradeStaticEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("image")] string? Image);

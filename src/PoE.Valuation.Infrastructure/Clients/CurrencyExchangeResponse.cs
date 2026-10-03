using System.Text.Json.Serialization;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Response of <c>GET {CurrencyExchangeBaseUrl}/{hourlyChangeId}</c>: the hourly currency-exchange
/// digest plus the cursor (<c>next_change_id</c>) for the next hour.
/// </summary>
public sealed record CurrencyExchangeResponse(
    [property: JsonPropertyName("next_change_id")] long NextChangeId,
    [property: JsonPropertyName("markets")] IReadOnlyList<MarketDigest> Markets);

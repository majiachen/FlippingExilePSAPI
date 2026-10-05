namespace PoE.Valuation.Core.Configuration;

/// <summary>Path of Exile public API settings. Bound from the "PathOfExile" configuration section.</summary>
public sealed class PoEApiOptions
{
    public const string SectionName = "PathOfExile";

    /// <summary>Base URL of the authenticated PoE API (league list + stash endpoints).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.pathofexile.com";

    /// <summary>
    /// Base URL of the public trade data endpoints. <c>GET {TradeDataBaseUrl}static</c> returns the
    /// item catalogue used to map an item name to the metadata path the snapshots are keyed by.
    /// Must end with a slash so the relative request URI resolves against it.
    /// </summary>
    public string TradeDataBaseUrl { get; init; } = "https://www.pathofexile.com/api/trade/data/";


    /// <summary>
    /// Base URL of the public currency-exchange endpoint. Requests are made to
    /// "{CurrencyExchangeBaseUrl}/{hourlyChangeId}". No authentication is required.
    /// </summary>
    public string CurrencyExchangeBaseUrl { get; init; } = "https://web.poecdn.com/api/currency-exchange";

    /// <summary>User-Agent header sent with every PoE API request.</summary>
    public string UserAgent { get; init; } = "PoEValuation/1.0";

    /// <summary>Per-request timeout in seconds for PoE API calls.</summary>
    public int RequestTimeoutSeconds { get; init; } = 30;
}
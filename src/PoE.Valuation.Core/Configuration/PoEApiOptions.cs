namespace PoE.Valuation.Core.Configuration;

/// <summary>Path of Exile public API settings. Bound from the "PathOfExile" configuration section.</summary>
public sealed class PoEApiOptions
{
    public const string SectionName = "PathOfExile";

    /// <summary>Base URL of the authenticated PoE API (stash endpoints).</summary>
    public string ApiBaseUrl { get; init; } = "https://api.pathofexile.com";

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
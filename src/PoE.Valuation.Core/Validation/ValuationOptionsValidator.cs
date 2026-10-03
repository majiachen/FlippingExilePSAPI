using PoE.Valuation.Core.Configuration;

namespace PoE.Valuation.Core.Validation;

/// <summary>
/// Validates the configuration required for startup. Returns a list of human-readable errors (empty = valid).
/// </summary>
public static class ValuationOptionsValidator
{
    public static IReadOnlyList<string> Validate(
        OAuthOptions oauth,
        RedisOptions redis,
        SqlOptions sql,
        PoEApiOptions poeApi)
    {
        ArgumentNullException.ThrowIfNull(oauth);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(poeApi);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(oauth.ClientId))
            errors.Add($"{OAuthOptions.SectionName}:ClientId is required.");
        if (string.IsNullOrWhiteSpace(oauth.ClientSecret))
            errors.Add($"{OAuthOptions.SectionName}:ClientSecret is required.");
        if (!IsHttpsAbsoluteUri(oauth.RedirectUri, out var redirectError))
            errors.Add(redirectError);

        if (string.IsNullOrWhiteSpace(redis.ConnectionString))
            errors.Add($"{RedisOptions.SectionName}:ConnectionString is required.");
        foreach (var (name, value) in new[]
        {
            (nameof(RedisOptions.SessionTtlDays), redis.SessionTtlDays),
            (nameof(RedisOptions.StashTtlMinutes), redis.StashTtlMinutes),
            (nameof(RedisOptions.HistoryCacheTtlMinutes), redis.HistoryCacheTtlMinutes),
            (nameof(RedisOptions.OAuthStateTtlMinutes), redis.OAuthStateTtlMinutes),
            (nameof(RedisOptions.PollingLockTtlMinutes), redis.PollingLockTtlMinutes),
            (nameof(RedisOptions.RateLimitWindowSeconds), redis.RateLimitWindowSeconds)
        })
        {
            if (value <= 0)
                errors.Add($"{RedisOptions.SectionName}:{name} must be positive.");
        }
        if (string.IsNullOrWhiteSpace(sql.ConnectionString))
            errors.Add($"{SqlOptions.SectionName}:ConnectionString is required.");

        if (!IsAbsoluteHttpOrHttpsUri(poeApi.ApiBaseUrl, "ApiBaseUrl", out var baseUrlError))
            errors.Add(baseUrlError);
        if (!IsAbsoluteHttpOrHttpsUri(poeApi.CurrencyExchangeBaseUrl, "CurrencyExchangeBaseUrl", out var currencyUrlError))
            errors.Add(currencyUrlError);

        return errors;
    }

    private static bool IsHttpsAbsoluteUri(string value, out string error)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(uri.Authority))
        {
            error = string.Empty;
            return true;
        }

        error = $"{OAuthOptions.SectionName}:RedirectUri must be an absolute https:// URI (PKCE requires a secure redirect).";
        return false;
    }

    private static bool IsAbsoluteHttpOrHttpsUri(string value, string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{PoEApiOptions.SectionName}:{name} is required.";
            return false;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            !string.IsNullOrWhiteSpace(uri.Authority))
        {
            error = string.Empty;
            return true;
        }

        error = $"{PoEApiOptions.SectionName}:{name} must be an absolute http(s):// URI.";
        return false;
    }
}

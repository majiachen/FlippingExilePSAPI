namespace PoE.Valuation.Core.Configuration;

/// <summary>
/// OAuth settings for the Path of Exile API client and the PKCE authorization-code flow.
/// Bound from the "OAuth" configuration section; legacy key names (ClientId, ClientSecret) are preserved.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";
    public const string ClientSecretKey = "ClientSecret";

    /// <summary>OAuth client id registered with Path of Exile.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>OAuth client secret. Must never be written to logs.</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Absolute https:// redirect URI used by the PKCE authorization-code flow.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>PoE OAuth authorization endpoint.</summary>
    public string AuthorizationEndpoint { get; init; } = "https://www.pathofexile.com/oauth/authorize";

    /// <summary>PoE OAuth token endpoint.</summary>
    public string TokenEndpoint { get; init; } = "https://www.pathofexile.com/oauth/token";

    /// <summary>Scopes requested from the PoE token endpoint.</summary>
    public string Scope { get; init; } = "service:psapi service:leagues service:cxapi";
}
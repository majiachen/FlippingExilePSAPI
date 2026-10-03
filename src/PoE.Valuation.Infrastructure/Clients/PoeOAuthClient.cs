using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Client for the PoE OAuth token endpoint (tech doc, section 5.1): exchanges an authorization
/// code plus PKCE verifier for an access/refresh token. The token endpoint is an absolute URL
/// configured via <c>OAuth:TokenEndpoint</c> and passed per call, so no base address is set.
/// </summary>
public sealed class PoeOAuthClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public PoeOAuthClient(HttpClient http) =>
        _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>
    /// Exchanges the authorization code for tokens (OAuth 2.0 authorization-code grant with PKCE).
    /// Throws <see cref="HttpRequestException"/> on any non-success status.
    /// </summary>
    public async Task<OAuthTokenResponse> ExchangeAuthorizationCodeAsync(
        string tokenEndpoint,
        string code,
        string codeVerifier,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken ct = default)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        using var response = await _http.PostAsync(tokenEndpoint, form, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("OAuth token response deserialized to null.");
    }

    /// <summary>
    /// Refreshes an access token using the stored refresh token (OAuth 2.0 refresh-token grant at
    /// the same token endpoint). PoE may rotate the refresh token, in which case the response carries
    /// a new one and the caller must persist it. Throws <see cref="HttpRequestException"/> on failure.
    /// </summary>
    public async Task<OAuthTokenResponse> RefreshTokenAsync(
        string tokenEndpoint,
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        using var response = await _http.PostAsync(tokenEndpoint, form, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("OAuth token refresh response deserialized to null.");
    }
}

/// <summary>OAuth 2.0 token response (PoE returns the standard OAuth fields plus <c>sub</c>/<c>username</c>).</summary>
public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] long ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("username")] string Username);

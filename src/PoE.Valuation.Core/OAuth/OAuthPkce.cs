using System.Security.Cryptography;
using System.Text;

namespace PoE.Valuation.Core.OAuth;

/// <summary>
/// One generated PKCE pair: the random <see cref="State"/> used in the authorization URL, the
/// <see cref="CodeVerifier"/> that must be stored server-side (Redis), and the S256
/// <see cref="CodeChallenge"/> that is sent to the authorization endpoint (RFC 7636, tech doc,
/// section 5.1).
/// </summary>
public sealed record PkcePair(string State, string CodeVerifier, string CodeChallenge);

/// <summary>
/// Pure PKCE helpers for the authorization-code flow: random state, verifier and S256 challenge
/// generation, state validation, and authorization-URI construction. No I/O, so it is fully
/// unit-testable.
/// </summary>
public static class OAuthPkce
{
    /// <summary>48 random bytes → 64 base64url chars, inside the RFC 7636 verifier range (43..128).</summary>
    private const int VerifierByteLength = 48;

    /// <summary>32 random bytes → 43 base64url chars of CSRF state.</summary>
    private const int StateByteLength = 32;

    /// <summary>Shortest base64url form of a generated state: (32 * 4 + 2) / 3 = 43 chars.</summary>
    private const int MinStateLength = (StateByteLength * 4 + 2) / 3;

    /// <summary>Generates a fresh state + verifier + S256 challenge from the CSPRNG.</summary>
    public static PkcePair Generate()
    {
        var verifier = ToBase64Url(RandomNumberGenerator.GetBytes(VerifierByteLength));
        var state = ToBase64Url(RandomNumberGenerator.GetBytes(StateByteLength));
        return new PkcePair(state, verifier, BuildChallenge(verifier));
    }

    /// <summary>Derives the S256 code challenge: base64url(SHA-256(ASCII(verifier))).</summary>
    public static string BuildChallenge(string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
            throw new ArgumentException("A code verifier is required.", nameof(codeVerifier));

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return ToBase64Url(hash);
    }

    /// <summary>
    /// True when <paramref name="state"/> is a well-formed base64url value of a plausible length.
    /// Malformed states are rejected before they can reach Redis key construction.
    /// </summary>
    public static bool IsValidState(string? state) =>
        state is not null
        && state.Length is >= MinStateLength and <= 128
        && state.All(IsBase64UrlChar);

    /// <summary>
    /// Builds the PoE authorization endpoint URL with the PKCE query parameters.
    /// </summary>
    public static Uri BuildAuthorizationUri(
        string authorizationEndpoint,
        string clientId,
        string redirectUri,
        string scope,
        string state,
        string codeChallenge)
    {
        var query = string.Join('&',
            QueryParameter("response_type", "code"),
            QueryParameter("client_id", clientId),
            QueryParameter("redirect_uri", redirectUri),
            QueryParameter("scope", scope),
            QueryParameter("state", state),
            QueryParameter("code_challenge", codeChallenge),
            QueryParameter("code_challenge_method", "S256"));

        return new Uri(authorizationEndpoint + "?" + query);
    }

    private static string QueryParameter(string key, string value) =>
        $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool IsBase64UrlChar(char c) =>
        c is >= 'A' and <= 'Z'
        or >= 'a' and <= 'z'
        or >= '0' and <= '9'
        or '-'
        or '_';
}

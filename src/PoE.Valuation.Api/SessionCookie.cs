using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace PoE.Valuation.Api;

/// <summary>
/// Name and semantics of the single API session cookie (tech doc, section 5.2). The cookie holds
/// a <c>sessionId</c> that maps to the PoE OAuth access token in Redis (<c>session:{id}</c>),
/// so the cookie value itself is never the secret.
/// </summary>
public static class SessionCookie
{
    /// <summary>Cookie name.</summary>
    public const string Name = "poe_sid";

    /// <summary>Cookie options: site-only, no JS access, survives restarts until expiry, TLS in production.</summary>
    public static CookieOptions Options { get; } = new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = true,
        Path = "/",
        MaxAge = TimeSpan.FromHours(8)
    };

    /// <summary>Reads the session id from the request cookie, or null when the cookie is absent.</summary>
    public static string? GetSessionId(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(Name, out var value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return null;
    }

    /// <summary>Writes the session cookie for <paramref name="sessionId"/> with the standard options.</summary>
    public static void Append(HttpResponse response, string sessionId) =>
        response.Cookies.Append(Name, sessionId, Options);

    /// <summary>Removes the session cookie (expiry in the past; the cookie name itself is retained).</summary>
    public static void Remove(HttpResponse response) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = true,
            Path = "/"
        });
}

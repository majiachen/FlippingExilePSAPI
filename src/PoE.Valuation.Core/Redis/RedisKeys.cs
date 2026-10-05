using System.Linq;

namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Server-side construction of every Redis key this backend uses. Keys are namespaced by
/// purpose and built exclusively from validated, server-known values — client-supplied key
/// names are never accepted (tech doc, section 3).
/// </summary>
public static class RedisKeys
{
    /// <summary>Service token for CX API polling. The only key stored without a TTL.</summary>
    public const string CxApiServiceTokenKey = "service:token:cxapi";

    /// <summary><c>oauth:state:{state}</c> — OAuth state + PKCE verifier. TTL: 10 min (Redis:OAuthStateTtlMinutes).</summary>
    public static string OAuthState(string state) => "oauth:state:" + ValidatePart(state);

    /// <summary><c>session:{sessionId}</c> — user session (tokens). TTL: 90 days (Redis:SessionTtlDays).</summary>
    public static string Session(string sessionId) => "session:" + ValidatePart(sessionId);

    /// <summary><c>session:{sessionId}:pending</c> — temporary target of an atomic token refresh, renamed over <see cref="Session"/>.</summary>
    public static string SessionPending(string sessionId) => Session(sessionId) + ":pending";

    /// <summary><c>stash:{sessionId}</c> — stash cache. TTL: 15 min (Redis:StashTtlMinutes).</summary>
    public static string Stash(string sessionId) => "stash:" + ValidatePart(sessionId);

    /// <summary><c>history:{itemId}:{range}</c> — history cache. TTL: 5 min (Redis:HistoryCacheTtlMinutes).</summary>
    public static string History(string itemId, string range) =>
        "history:" + ValidatePart(itemId) + ":" + ValidatePart(range);

    /// <summary><c>lock:polling:{leagueId}</c> — polling distributed lock. TTL: 5 min, auto-renewed while held (Redis:PollingLockTtlMinutes).</summary>
    public static string PollingLock(int leagueId) => $"lock:polling:{leagueId}";

    /// <summary><c>ratelimit:{sessionId}:{endpoint}</c> — rate-limit counter. TTL: 60 s (Redis:RateLimitWindowSeconds).</summary>
    public static string RateLimit(string sessionId, string endpoint) =>
        "ratelimit:" + ValidatePart(sessionId) + ":" + ValidatePart(endpoint);

    /// <summary><c>league:{leagueName}</c> — cached PoE league name → league id mapping (the stash endpoint needs the league id). TTL: 24 h, set by the resolver.</summary>
    public static string LeagueMapping(string leagueName) => "league:" + ValidatePart(leagueName);

    /// <summary><c>lock:item-catalog</c> — single-writer lock over the item catalogue refresh. TTL: 10 min, auto-renewed while held.</summary>
    public const string ItemCatalogLockKey = "lock:item-catalog";


    /// <summary>Rejects empty parts and parts containing ':' or any control character so keys stay flat and unambiguous.</summary>
    private static string ValidatePart(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
            throw new ArgumentException("A Redis key part must not be empty.", nameof(part));
        if (part.Any(c => c == ':' || char.IsControl(c)))
            throw new ArgumentException("A Redis key part must not contain ':' or control characters.", nameof(part));
        return part;
    }
}
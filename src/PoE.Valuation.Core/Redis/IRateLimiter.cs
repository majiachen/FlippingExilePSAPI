namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Per-session, fixed-window rate limiter backed by Redis counters (tech doc, section 5.2).
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Counts one request against <paramref name="endpoint"/> for <paramref name="sessionId"/>
    /// (key <c>ratelimit:{sessionId}:{endpoint}</c>, window <paramref name="window"/>).
    /// Returns false once the window's budget of <paramref name="maxRequests"/> is exhausted —
    /// callers should respond with HTTP 429.
    /// </summary>
    Task<bool> TryAcquireAsync(string sessionId, string endpoint, int maxRequests, TimeSpan window, CancellationToken ct = default);
}
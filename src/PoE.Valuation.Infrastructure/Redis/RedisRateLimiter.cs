using PoE.Valuation.Core.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>
/// Per-session, fixed-window rate limiter (tech doc, section 5.2): a Redis counter per
/// (session, endpoint) pair that expires once per window. For a side project this is
/// sufficient; production-grade sliding-window limiting would use a Lua script with sorted sets.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IRedisStore _store;

    public RedisRateLimiter(IRedisStore store) => _store = store;

    public async Task<bool> TryAcquireAsync(string sessionId, string endpoint, int maxRequests, TimeSpan window, CancellationToken ct = default)
    {
        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), maxRequests, "maxRequests must be positive.");
        if (window <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(window), window, "window must be positive.");

        var count = await _store.IncrementAsync(RedisKeys.RateLimit(sessionId, endpoint), window, ct);
        return count <= maxRequests;
    }
}
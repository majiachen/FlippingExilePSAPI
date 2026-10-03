namespace PoE.Valuation.Core.Redis;

/// <summary>Redis-backed cache of a user's public stash (tech doc, section 4.2; key <c>stash:{sessionId}</c>, TTL 15 min).</summary>
public interface IStashCache
{
    /// <summary>Reads the cache, or null when it does not exist or has expired.</summary>
    Task<StashEntry?> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Stores the cache with the stash TTL (15 minutes by default).</summary>
    Task SetAsync(string sessionId, StashEntry entry, CancellationToken ct = default);
}
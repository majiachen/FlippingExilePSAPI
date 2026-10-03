namespace PoE.Valuation.Core.Redis;

/// <summary>
/// A distributed lock backed by Redis (SET key value NX PX), so an hourly job can run
/// exactly once across all instances (tech doc, section 5.1).
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Tries to acquire the lock for <paramref name="resourceKey"/>. A unique owner id is generated
    /// per acquisition, and held locks are renewed automatically until released or lost.
    /// Returns false when another instance holds the lock.
    /// </summary>
    Task<bool> TryAcquireAsync(string resourceKey, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Releases a lock held by this instance (owner-checked via a Lua script). Releasing a lock
    /// this instance does not hold is a no-op — never release another instance's lock.
    /// </summary>
    Task ReleaseAsync(string resourceKey, CancellationToken ct = default);
}
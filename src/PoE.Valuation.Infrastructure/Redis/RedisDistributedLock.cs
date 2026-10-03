using System.Collections.Concurrent;
using PoE.Valuation.Core.Redis;
using StackExchange.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>
/// Redis-backed distributed lock (tech doc, section 5.1). Acquired with
/// SET key owner NX PX and released with an owner-checked Lua script so an instance can never
/// release another instance's lock. Held locks are renewed automatically (every ttl/3) until
/// released or lost, so a long-running polling cycle keeps its lock.
/// </summary>
public sealed class RedisDistributedLock : IDistributedLock
{
    // Releases the lock only when the caller still owns it.
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    // Extends the TTL only when the caller still owns it; 0 means the lock was lost.
    private const string RenewScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('PEXPIRE', KEYS[1], ARGV[2])
        else
            return 0
        end
        """;

    private readonly IRedisStore _store;
    private readonly ConcurrentDictionary<string, HeldLock> _held = new();

    public RedisDistributedLock(IRedisStore store) => _store = store;

    public async Task<bool> TryAcquireAsync(string resourceKey, TimeSpan ttl, CancellationToken ct = default)
    {
        if (_held.ContainsKey(resourceKey))
            return true; // this instance already holds the lock

        // A unique owner id per acquisition (machine name + random suffix) so the owner-checked
        // release can never match another instance.
        var ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var acquired = await _store.SetIfNotExistsAsync(resourceKey, ownerId, ttl, ct);
        if (!acquired)
            return false;

        _held[resourceKey] = new HeldLock(_store, resourceKey, ownerId, ttl);
        return true;
    }

    public async Task ReleaseAsync(string resourceKey, CancellationToken ct = default)
    {
        if (!_held.TryRemove(resourceKey, out var held))
            return; // not held by this instance — never release another instance's lock

        held.StopRenewing();
        ct.ThrowIfCancellationRequested();
        await _store.Db.ScriptEvaluateAsync(
            ReleaseScript,
            new RedisKey[] { resourceKey },
            new RedisValue[] { held.OwnerId },
            CommandFlags.None);
    }

    private sealed class HeldLock
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _renewal;

        public HeldLock(IRedisStore store, string resourceKey, string ownerId, TimeSpan ttl)
        {
            OwnerId = ownerId;
            _renewal = RenewAsync(store, resourceKey, ttl);
        }

        public string OwnerId { get; }

        public void StopRenewing() => _cts.Cancel();

        private async Task RenewAsync(IRedisStore store, string resourceKey, TimeSpan ttl)
        {
            // Renew every third of the TTL; the renew script restores the full TTL while we own it.
            var interval = ttl / 3;
            try
            {
                while (true)
                {
                    await Task.Delay(interval, _cts.Token);
                    var renewed = (long)await store.Db.ScriptEvaluateAsync(
                        RenewScript,
                        new RedisKey[] { resourceKey },
                        new RedisValue[] { OwnerId, (long)ttl.TotalMilliseconds },
                        CommandFlags.None);
                    if (renewed != 1)
                        break; // the lock expired or was taken over — stop renewing
                }
            }
            catch (OperationCanceledException)
            {
                // The lock was released while renewing; nothing to do.
            }
            catch (Exception)
            {
                // Redis hiccups must not take the process down: the lock simply falls back to
                // expiring by TTL until the next successful renew.
            }
        }
    }
}
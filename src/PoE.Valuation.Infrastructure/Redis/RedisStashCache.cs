using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>Redis-backed stash cache (tech doc, section 4.2; key <c>stash:{sessionId}</c>, TTL 15 min).</summary>
public sealed class RedisStashCache : IStashCache
{
    private readonly IRedisStore _store;
    private readonly RedisOptions _options;

    public RedisStashCache(IRedisStore store, IOptions<RedisOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public Task<StashEntry?> GetAsync(string sessionId, CancellationToken ct = default) =>
        _store.GetAsync<StashEntry>(RedisKeys.Stash(sessionId), ct);

    public Task SetAsync(string sessionId, StashEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _store.SetAsync(RedisKeys.Stash(sessionId), entry, _options.StashTtl, ct);
    }
}
using PoE.Valuation.Core.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>
/// Cached service token for CX API polling (tech doc, section 4.2; key <c>service:token:cxapi</c>).
/// Stored without a TTL — the only persistent key; it is simply replaced when a fresh token
/// is obtained.
/// </summary>
public sealed class RedisServiceTokenProvider : IServiceTokenProvider
{
    private readonly IRedisStore _store;

    public RedisServiceTokenProvider(IRedisStore store) => _store = store;

    public Task<ServiceTokenEntry?> GetAsync(CancellationToken ct = default) =>
        _store.GetAsync<ServiceTokenEntry>(RedisKeys.CxApiServiceTokenKey, ct);

    public Task SetAsync(ServiceTokenEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _store.SetAsync(RedisKeys.CxApiServiceTokenKey, entry, ttl: null, ct);
    }
}
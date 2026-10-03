using System.Text.Json;
using System.Text.Json.Serialization;
using PoE.Valuation.Core.Redis;
using StackExchange.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>
/// <see cref="IRedisStore"/> over a single shared <see cref="IDatabase"/> handle (tech doc,
/// sections 2 and 4). All values are serialized with System.Text.Json as camelCase, null-free
/// UTF-8 JSON strings so they stay human-readable in Redis CLI.
/// </summary>
/// <remarks>
/// StackExchange.Redis commands do not accept cancellation tokens (command timeouts come from
/// the multiplexer configuration), so the caller's token is honored cooperatively: every method
/// throws <see cref="OperationCanceledException"/> when its token is already canceled before the
/// command is issued.
/// </remarks>
public sealed class RedisStore : IRedisStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConnectionMultiplexer _connection;

    public RedisStore(IConnectionMultiplexer connection) => _connection = connection;

    public IDatabase Db => _connection.GetDatabase(0, CommandFlags.None);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var raw = await Db.StringGetAsync(key, CommandFlags.None);
        if (raw.IsNull)
            return default;
        return JsonSerializer.Deserialize<T>(raw.ToString(), JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await Db.StringSetAsync(key, json, ttl, When.Always, CommandFlags.None);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await Db.KeyDeleteAsync(key, CommandFlags.None);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await Db.KeyExistsAsync(key, CommandFlags.None);
    }

    public async Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        // SET key value NX PX: true only when the key was actually created.
        return await Db.StringSetAsync(key, value, ttl, When.NotExists, CommandFlags.None);
    }

    public async Task<long> IncrementAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var count = await Db.StringIncrementAsync(key, 1, CommandFlags.None);
        if (count == 1)
            await Db.KeyExpireAsync(key, ttl, ExpireWhen.Always, CommandFlags.None);
        return count;
    }

    public async Task<bool> RenameAsync(string fromKey, string toKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await Db.KeyRenameAsync(fromKey, toKey, When.Always, CommandFlags.None);
    }
}
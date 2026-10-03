using StackExchange.Redis;

namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Low-level Redis store used by all higher-level stores (tech doc, section 2.3).
/// Values are stored as UTF-8 JSON strings so they stay human-readable in Redis CLI.
/// Connection failures surface as <see cref="RedisConnectionException"/>; the API layer
/// translates those into HTTP 503 (tech doc, section 6.1).
/// </summary>
public interface IRedisStore
{
    /// <summary>
    /// The underlying database handle for operations the generic helpers do not cover
    /// (e.g. <c>KeyExpire</c>, <c>ScriptEvaluate</c>). One shared database handle per app.
    /// </summary>
    IDatabase Db { get; }

    /// <summary>Reads and deserializes a value; returns null when the key does not exist.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Serializes and stores a value, optionally with a TTL. Ephemeral values must always pass a TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>Deletes a key. Returns true when the key existed.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Stores <paramref name="value"/> only when <paramref name="key"/> does not exist yet (SET key value NX PX).</summary>
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Increments a counter and sets its TTL on the first increment (rate-limit counters).
    /// Returns the value after incrementing.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Atomically renames a key (RENAME). Returns true when the source key existed.</summary>
    Task<bool> RenameAsync(string fromKey, string toKey, CancellationToken ct = default);
}
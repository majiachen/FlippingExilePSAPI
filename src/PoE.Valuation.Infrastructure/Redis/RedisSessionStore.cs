using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Logging;
using PoE.Valuation.Core.Redis;
using StackExchange.Redis;

namespace PoE.Valuation.Infrastructure.Redis;

/// <summary>
/// Redis-backed user sessions (tech doc, sections 4.2 and 5.3). Session ids are registered with
/// the <see cref="SensitiveValueStore"/> so they can never appear in log output; logs reference
/// the user's <c>sub</c> only.
/// </summary>
public sealed class RedisSessionStore : ISessionStore
{
    /// <summary>Lifetime of the temporary pending entry during an atomic token refresh (tech doc, section 5.3).</summary>
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(5);

    private readonly IRedisStore _store;
    private readonly RedisOptions _options;
    private readonly SensitiveValueStore _sensitiveValues;
    private readonly ILogger<RedisSessionStore> _logger;

    public RedisSessionStore(
        IRedisStore store,
        IOptions<RedisOptions> options,
        SensitiveValueStore sensitiveValues,
        ILogger<RedisSessionStore> logger)
    {
        _store = store;
        _options = options.Value;
        _sensitiveValues = sensitiveValues;
        _logger = logger;
    }

    public Task<SessionEntry?> GetAsync(string sessionId, CancellationToken ct = default) =>
        _store.GetAsync<SessionEntry>(RedisKeys.Session(sessionId), ct);

    public Task SetAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _sensitiveValues.Add(sessionId);
        return _store.SetAsync(RedisKeys.Session(sessionId), entry, _options.SessionTtl, ct);
    }

    public async Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var deleted = await _store.DeleteAsync(RedisKeys.Session(sessionId), ct);
        if (deleted)
            _sensitiveValues.Remove(sessionId);
        return deleted;
    }

    public async Task ReplaceAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var key = RedisKeys.Session(sessionId);
        var pendingKey = RedisKeys.SessionPending(sessionId);

        _sensitiveValues.Add(sessionId);

        // Write to a temporary key first: if the process dies before the rename, the pending
        // entry expires and the previous session stays intact — a failed refresh never logs
        // the user out. RENAME itself is atomic.
        await _store.SetAsync(pendingKey, entry, PendingTtl, ct);
        if (!await _store.RenameAsync(pendingKey, key, ct))
            throw new InvalidOperationException("Session replacement failed: the pending entry was not found.");

        // The rename carries the pending entry's short TTL; restore the full session TTL. A crash
        // between the rename and this call leaves a shorter (but still valid) session.
        await _store.Db.KeyExpireAsync(key, _options.SessionTtl, ExpireWhen.Always, CommandFlags.None);
        _logger.LogDebug("Session tokens replaced for user sub {Sub}", entry.Sub);
    }
}
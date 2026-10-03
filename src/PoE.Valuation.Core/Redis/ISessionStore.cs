namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Redis-backed storage of user sessions (OAuth tokens) keyed by session id
/// (tech doc, sections 4.2 and 5.3).
/// </summary>
public interface ISessionStore
{
    /// <summary>Reads the session, or null when it does not exist or has expired.</summary>
    Task<SessionEntry?> GetAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Stores (or overwrites) the session with the session TTL (90 days by default).</summary>
    Task SetAsync(string sessionId, SessionEntry entry, CancellationToken ct = default);

    /// <summary>Deletes the session. Returns true when the key existed.</summary>
    Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Atomically replaces the session's tokens after a refresh (tech doc, section 5.3): the new
    /// entry is written to a short-lived pending key and atomically renamed over the existing
    /// session, so a failure mid-refresh never leaves the user logged out.
    /// </summary>
    Task ReplaceAsync(string sessionId, SessionEntry entry, CancellationToken ct = default);
}
namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Data models stored in Redis as UTF-8 JSON strings (tech doc, section 4). They are serialized
/// with camelCase property names and nulls omitted so values stay human-readable in Redis CLI.
/// </summary>

/// <summary>Stored at <c>session:{sessionId}</c> (TTL 90 days). The session id is the key itself, not a field.</summary>
public sealed record SessionEntry(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string Sub,
    string Username,
    DateTimeOffset CreatedAt);

/// <summary>Stored at <c>stash:{sessionId}</c> (TTL 15 min).</summary>
public sealed record StashEntry(
    IReadOnlyList<StashItem> Items,
    DateTimeOffset LastRefreshedAt,
    int LeagueId);

/// <summary>
/// A cached projection of one public stash item — only the fields the valuation logic needs,
/// kept small so stash entries stay well under the 100 KB value-size guideline (tech doc, section 4.1).
/// </summary>
public sealed record StashItem(
    string ItemId,
    string League,
    string Name,
    string BaseType,
    /// <summary>Frame type: 0 normal, 1 magic, 2 rare, 3 unique, 4 gem, 5 currency.</summary>
    int FrameType,
    int ItemLevel,
    int? StackSize,
    int? GemLevel,
    int? GemQuality,
    bool Corrupted,
    bool Verified,
    string? Note);

/// <summary>Stored at <c>oauth:state:{state}</c> (TTL 10 min).</summary>
public sealed record OAuthStateEntry(
    string CodeVerifier,
    DateTimeOffset CreatedAt);

/// <summary>Stored at <c>service:token:cxapi</c> (no TTL — cached indefinitely, replaced on refresh).</summary>
public sealed record ServiceTokenEntry(
    string AccessToken,
    DateTimeOffset ObtainedAt);
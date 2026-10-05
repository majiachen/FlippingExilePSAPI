namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// The user's stash for a league (GET /api/stash?league=...). The response is a snapshot: it may
/// be served from the per-session Redis cache when fresh (tech doc, section 5.3).
/// </summary>
/// <param name="League">League name the stash was fetched for (e.g. <c>Standard</c>).</param>
/// <param name="LeagueId">PoE league id the stash was fetched from — the league name itself (e.g. <c>Solo Self-Found</c>).</param>

/// <param name="LastRefreshedAt">When the cached stash snapshot was last refreshed, UTC.</param>
/// <param name="Count">Number of items in <see cref="Items"/>.</param>
/// <param name="Items">All items in the account's stash tabs for the league.</param>
/// <param name="Cached">True when the response was served from the Redis stash cache.</param>
public sealed record StashResponse(
    string League,
    string LeagueId,

    DateTimeOffset LastRefreshedAt,
    int Count,
    IReadOnlyList<StashItemDto> Items,
    bool Cached);

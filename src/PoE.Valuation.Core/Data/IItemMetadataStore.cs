namespace PoE.Valuation.Core.Data;

/// <summary>
/// Read/write access to the item catalogue that maps a human item name to the metadata path the
/// currency-exchange snapshots are keyed by (the gap the valuation endpoints alone cannot close:
/// the stash API publishes <c>name</c>/<c>base_type</c>, never a metadata path).
/// </summary>
public interface IItemMetadataStore
{
    /// <summary>
    /// Returns catalogue candidates for a <em>normalized</em> lookup key: exact, prefix and
    /// substring matches, plus trade-alias matches. Candidates are deliberately generous — the
    /// final ranking and truncation is done by <see cref="Validation.ItemNameMatcher"/>.
    /// </summary>
    Task<IReadOnlyList<ItemMetadataEntry>> SearchCandidatesAsync(
        string lookupKey, int limit, CancellationToken ct = default);

    /// <summary>
    /// Market partners of <paramref name="metadata"/> in <paramref name="league"/> that have a
    /// snapshot within the latest-valuation look-back window, one row per partner (the newest
    /// snapshot of that pair), ordered by reference display name.
    /// </summary>
    Task<IReadOnlyList<ItemMarketOption>> GetMarketOptionsAsync(
        string metadata, string league, int limit, CancellationToken ct = default);

    /// <summary>
    /// Atomically replaces the whole catalogue with <paramref name="entries"/> (the refresh is a
    /// full swap, not a merge: the upstream catalogue is the complete current item list).
    /// </summary>
    /// <returns>Number of rows written.</returns>
    Task<int> ReplaceCatalogAsync(
        IReadOnlyList<ItemMetadataEntry> entries, DateTimeOffset refreshedAtUtc, CancellationToken ct = default);

    /// <summary>When the catalogue was last refreshed, UTC; null when it has never been refreshed.</summary>
    Task<DateTimeOffset?> GetCatalogUpdatedAtAsync(CancellationToken ct = default);

    /// <summary>Number of catalogue rows currently stored.</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}

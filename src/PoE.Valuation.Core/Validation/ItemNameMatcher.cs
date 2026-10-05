using PoE.Valuation.Core.Data;

namespace PoE.Valuation.Core.Validation;

/// <summary>
/// Turns the raw candidates a store returns into the ranked match list a client gets back. Pure and
/// deterministic: the same candidates and query always produce the same order, which is what makes
/// the lookup safe to expose to a frontend that has to pick one item.
/// </summary>
public static class ItemNameMatcher
{
    /// <summary>
    /// Classifies one candidate against a query. A query that is already a metadata path
    /// (<c>Metadata/…</c>) matches only an entry with that exact metadata path.
    /// </summary>
    public static ItemMatchKind Classify(string query, ItemMetadataEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var normalizedQuery = ItemNameNormalizer.Normalize(query);
        if (normalizedQuery.Length == 0)
            return ItemMatchKind.None;

        if (IsMetadataPath(query))
            return string.Equals(query.Trim(), entry.Metadata, StringComparison.OrdinalIgnoreCase)
                ? ItemMatchKind.Exact
                : ItemMatchKind.None;

        if (string.Equals(normalizedQuery, entry.LookupKey, StringComparison.Ordinal))
            return ItemMatchKind.Exact;

        if (ItemNameNormalizer.Normalize(entry.TradeAlias) is { Length: > 0 } alias
            && string.Equals(normalizedQuery, alias, StringComparison.Ordinal))
            return ItemMatchKind.Alias;

        if (entry.LookupKey.StartsWith(normalizedQuery, StringComparison.Ordinal)
            || normalizedQuery.StartsWith(entry.LookupKey, StringComparison.Ordinal))
            return ItemMatchKind.Prefix;

        if (entry.LookupKey.Contains(normalizedQuery, StringComparison.Ordinal))
            return ItemMatchKind.Contains;

        return ItemMatchKind.None;
    }

    /// <summary>
    /// Ranks <paramref name="candidates"/> for <paramref name="query"/>: strongest match kind first,
    /// then the shortest lookup key (a more specific name wins over a longer one that merely
    /// contains the query), then the metadata path so ties are stable across calls.
    /// Candidates that do not match at all are dropped.
    /// </summary>
    public static IReadOnlyList<ItemMetadataMatch> Rank(
        string query, IEnumerable<ItemMetadataEntry> candidates, int limit)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (limit <= 0)
            return Array.Empty<ItemMetadataMatch>();

        return candidates
            .Select(entry => (Entry: entry, Kind: Classify(query, entry)))
            .Where(x => x.Kind != ItemMatchKind.None)
            .OrderBy(x => (int)x.Kind)
            .ThenBy(x => x.Entry.LookupKey.Length)
            .ThenBy(x => x.Entry.Metadata, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ItemMetadataMatch(
                x.Entry.Metadata, x.Entry.DisplayName, x.Entry.Category, x.Entry.TradeAlias, x.Kind))
            .ToList();
    }

    /// <summary>
    /// True when the client already supplied a metadata path rather than a display name, in which
    /// case no catalogue resolution is needed.
    /// </summary>
    public static bool IsMetadataPath(string? value) =>
        value is { Length: > 0 } && value.Trim().StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase);
}

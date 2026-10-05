namespace PoE.Valuation.Core.Data;

/// <summary>
/// One entry of the PoE item catalogue: the mapping from a human item name (what the stash API
/// publishes as <c>name</c>/<c>base_type</c>) to the metadata path the currency-exchange snapshots
/// are keyed by. See <see cref="ItemMetadataEntry.Metadata"/> for the exact form.
/// </summary>
/// <param name="Metadata">
/// PoE metadata path, e.g. <c>Metadata/Items/Currency/CurrencyRerollRare</c>. This is the value
/// <c>exchange_rate_snapshots.currency_a</c>/<c>currency_b</c> store, so it can be passed to
/// <see cref="IMarketValuationStore"/> unchanged.
/// </param>
/// <param name="DisplayName">Item name as published by PoE (e.g. <c>Chaos Orb</c>).</param>
/// <param name="Category">Catalogue category id (e.g. <c>Currency</c>, <c>Maps</c>).</param>
/// <param name="TradeAlias">
/// The trade site's short item alias (e.g. <c>chaos</c>), when published. Accepted as a search term.
/// </param>
/// <param name="LookupKey">
/// <see cref="Validation.ItemNameNormalizer.Normalize"/> of <paramref name="DisplayName"/>; the value
/// the lookup index is keyed by.
/// </param>
public sealed record ItemMetadataEntry(
    string Metadata,
    string DisplayName,
    string Category,
    string? TradeAlias,
    string LookupKey);

/// <summary>
/// How strongly a query matched a catalogue entry, in decreasing confidence. Used to rank lookup
/// results so the frontend can tell an exact item from a prefix guess.
/// </summary>
public enum ItemMatchKind
{
    /// <summary>Normalized query equals the entry's lookup key (or the query is a metadata path).</summary>
    Exact = 0,

    /// <summary>Query equals the entry's trade alias (e.g. <c>chaos</c> for Chaos Orb).</summary>
    Alias = 1,

    /// <summary>Query is a prefix of the entry's lookup key, or the reverse.</summary>
    Prefix = 2,

    /// <summary>Query occurs inside the entry's lookup key.</summary>
    Contains = 3,

    /// <summary>No match; the entry is not returned by the ranker.</summary>
    None = 4
}

/// <summary>A ranked catalogue match returned by <see cref="Validation.ItemNameMatcher"/>.</summary>
public sealed record ItemMetadataMatch(
    string Metadata,
    string DisplayName,
    string Category,
    string? TradeAlias,
    ItemMatchKind Kind);

/// <summary>
/// A reference currency the item can actually be valued against: one market partner of
/// <c>metadata</c> in the league, with the most recent hourly snapshot of that pair.
/// </summary>
/// <param name="Reference">Reference item metadata path to pass as <c>reference</c> to the valuation endpoints.</param>
/// <param name="ReferenceDisplayName">Reference item name, when the catalogue knows it; otherwise the metadata path.</param>
/// <param name="SnapshotHourUtc">Start of the most recent digest hour for this pair, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="HighestRate">Highest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record ItemMarketOption(
    string Reference,
    string ReferenceDisplayName,
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);

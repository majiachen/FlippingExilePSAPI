namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// One side of a by-name valuation: what the client asked for, and the item id it resolved to.
/// </summary>
/// <param name="Input">The value the client supplied (a name, a trade alias or a metadata path).</param>
/// <param name="ItemId">Metadata path the value resolved to and that the snapshot lookup used.</param>
/// <param name="DisplayName">Catalogue name of the resolved item, or the metadata path when it was supplied directly.</param>
/// <param name="MatchKind">Exact | Alias | Prefix | Contains.</param>
/// <param name="Alternatives">
/// Other metadata paths that matched the same input, strongest first. Empty when the resolution was
/// unambiguous; a frontend should offer these when the resolved item is not the one the user meant.
/// </param>
public sealed record ResolvedItemDto(
    string Input,
    string ItemId,
    string DisplayName,
    string MatchKind,
    IReadOnlyList<string> Alternatives);

/// <summary>
/// Latest valuation of an item named the way a stash item is named (GET /api/valuation/by-name/{itemName}).
/// Same data as <see cref="ValuationResponse"/>, plus the name → metadata resolution that produced it.
/// Rates are units of the reference per 1 unit of the item, as observed over the snapshot hour.
/// </summary>
/// <param name="Item">Resolved item side.</param>
/// <param name="Reference">Resolved reference side.</param>
/// <param name="League">League name (e.g. <c>Standard</c>).</param>
/// <param name="MarketId">The compound <c>itemA|itemB</c> market key the data point came from.</param>
/// <param name="SnapshotHourUtc">Start of the digest hour, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="HighestRate">Highest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record ItemValuationResponse(
    ResolvedItemDto Item,
    ResolvedItemDto Reference,
    string League,
    string MarketId,
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);

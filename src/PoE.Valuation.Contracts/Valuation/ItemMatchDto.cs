namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// One reference currency an item can actually be valued against, with the latest hourly snapshot
/// of that market pair (GET /api/items/lookup).
/// </summary>
/// <param name="Reference">Reference metadata path to send as <c>reference</c> to /api/valuation/{itemId}.</param>
/// <param name="ReferenceDisplayName">Reference item name, or its metadata path when the catalogue does not name it.</param>
/// <param name="SnapshotHourUtc">Start of the most recent digest hour for this pair, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="HighestRate">Highest observed rate this hour (units of reference per 1 item), when available.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record ItemMarketOptionDto(
    string Reference,
    string ReferenceDisplayName,
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);

/// <summary>
/// One catalogue match: a stash item name resolved to the metadata path the valuation endpoints
/// accept (GET /api/items/lookup).
/// </summary>
/// <param name="ItemId">Metadata path to pass to /api/valuation/{itemId} or /api/history/{itemId}.</param>
/// <param name="DisplayName">Item name as PoE publishes it.</param>
/// <param name="Category">Catalogue category id (e.g. <c>Currency</c>).</param>
/// <param name="TradeAlias">Trade site item alias (e.g. <c>chaos</c>), when known.</param>
/// <param name="MatchKind">Exact | Alias | Prefix | Contains — how confidently the name resolved.</param>
/// <param name="Markets">Reference currencies with market data for this item; empty when no league was requested.</param>
public sealed record ItemMatchDto(
    string ItemId,
    string DisplayName,
    string Category,
    string? TradeAlias,
    string MatchKind,
    IReadOnlyList<ItemMarketOptionDto> Markets);

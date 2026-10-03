namespace PoE.Valuation.Contracts.Valuation;

/// <summary>
/// Latest known hourly valuation of <see cref="ItemId"/> in terms of <see cref="ReferenceItemId"/>
/// (GET /api/valuation/{itemId}). Rates are units of the reference item per 1 unit of the item,
/// as observed over the snapshot hour.
/// </summary>
/// <param name="ItemId">The item id being valued.</param>
/// <param name="ReferenceItemId">The reference item id the value is expressed in.</param>
/// <param name="League">League name (e.g. <c>Standard</c>).</param>
/// <param name="MarketId">The compound <c>itemA|itemB</c> market key the data point came from.</param>
/// <param name="SnapshotHourUtc">Start of the digest hour, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour (units of the reference per 1 item), when available.</param>
/// <param name="HighestRate">Highest observed rate this hour (units of the reference per 1 item), when available.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record ValuationResponse(
    string ItemId,
    string ReferenceItemId,
    string League,
    string MarketId,
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);

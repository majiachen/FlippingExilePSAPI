namespace PoE.Valuation.Core.Data;

/// <summary>
/// One hourly data point for the valuation of <c>itemId</c> in terms of <c>reference</c>, derived
/// from one <c>exchange_rate_snapshots</c> row (tech doc, section 7).
/// </summary>
/// <param name="MarketId">The compound <c>itemA|itemB</c> market key of the snapshot row.</param>
/// <param name="SnapshotHourUtc">Start of the digest hour, UTC.</param>
/// <param name="LowestRate">Lowest observed rate this hour: units of the reference item per 1 unit of the item.</param>
/// <param name="HighestRate">Highest observed rate this hour: units of the reference item per 1 unit of the item.</param>
/// <param name="VolumeItem">Units of the item traded in the hour, when published.</param>
/// <param name="VolumeReference">Units of the reference item traded in the hour, when published.</param>
public sealed record ValuationPoint(
    string MarketId,
    DateTimeOffset SnapshotHourUtc,
    double? LowestRate,
    double? HighestRate,
    long? VolumeItem,
    long? VolumeReference);

/// <summary>
/// Read-side access to the hourly currency-exchange snapshots the poller writes to
/// <c>exchange_rate_snapshots</c>. Values are parsed from <c>metrics_json</c> with the exact
/// serialization the poller uses, so readers and writers cannot drift.
/// </summary>
public interface IMarketValuationStore
{
    /// <summary>
    /// Returns the latest snapshot (within the last 24 hours) of the item/reference pair in the
    /// league, in either market direction, or null when no data exists yet.
    /// </summary>
    Task<ValuationPoint?> GetLatestAsync(string itemId, string reference, string league, CancellationToken ct = default);

    /// <summary>
    /// Returns up to <paramref name="maxPoints"/> hourly snapshots of the item/reference pair in
    /// the league (either market direction) within the last <paramref name="window"/>, newest
    /// first.
    /// </summary>
    Task<IReadOnlyList<ValuationPoint>> GetHistoryAsync(
        string itemId,
        string reference,
        string league,
        TimeSpan window,
        int maxPoints,
        CancellationToken ct = default);
}

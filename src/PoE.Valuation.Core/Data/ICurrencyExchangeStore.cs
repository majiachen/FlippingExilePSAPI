namespace PoE.Valuation.Core.Data;

/// <summary>One market-pair row from a single hourly currency-exchange digest.</summary>
public sealed record MarketSnapshotRow(
    DateTimeOffset SnapshotHourUtc,
    string League,
    string MarketId,
    string CurrencyA,
    string CurrencyB,
    string MetricsJson);

/// <summary>Persistence for hourly currency-exchange snapshots and the polling cursor.</summary>
public interface ICurrencyExchangeStore
{
    /// <summary>Returns the last committed <c>next_change_id</c>, or null when nothing has been committed yet.</summary>
    Task<long?> ReadCursorAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically stores one hour of market rows and advances the polling cursor.
    /// The cursor update is optimistic: it only applies while the stored cursor still equals
    /// <paramref name="expectedCursor"/> (null matches a never-committed cursor).
    /// </summary>
    /// <returns>true when committed; false when another poller advanced the cursor first.</returns>
    Task<bool> CommitHourAsync(
        IReadOnlyList<MarketSnapshotRow> rows,
        long? expectedCursor,
        long newCursor,
        CancellationToken cancellationToken = default);
}

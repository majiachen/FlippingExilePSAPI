using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Data;

/// <summary>
/// Dapper-based read implementation of <see cref="IMarketValuationStore"/> (tech doc, section 7):
/// resolves the snapshot row for an item/reference pair in either market direction via the
/// denormalized <c>currency_a</c>/<c>currency_b</c> columns and parses <c>metrics_json</c> back
/// into a <see cref="MarketDigest"/> using the same default <see cref="JsonSerializer"/> options
/// the poller serializes with, so readers and writers cannot drift.
/// </summary>
public sealed class PostgresMarketValuationStore : IMarketValuationStore
{
    /// <summary>
    /// Mirrors <c>JsonSerializer.Serialize(digest)</c> in <c>CurrencyExchangePoller</c> — default
    /// options, the <c>[JsonPropertyName]</c> attributes on <see cref="MarketDigest"/> producing the
    /// snake_case JSON stored in the column.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>Look-back window for "latest": a snapshot at most this old still counts as current (hourly cadence + margin).</summary>
    private static readonly TimeSpan LatestLookback = TimeSpan.FromHours(24);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresMarketValuationStore> _logger;

    public PostgresMarketValuationStore(IDbConnectionFactory connectionFactory, ILogger<PostgresMarketValuationStore> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ValuationPoint?> GetLatestAsync(string itemId, string reference, string league, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var points = await QueryPointsAsync(itemId, reference, league, DateTimeOffset.UtcNow - LatestLookback, 1, ct);
        return points.Count > 0 ? points[0] : null;
    }

    public Task<IReadOnlyList<ValuationPoint>> GetHistoryAsync(
        string itemId, string reference, string league, TimeSpan window, int maxPoints, CancellationToken ct = default)
        => QueryPointsAsync(itemId, reference, league, DateTimeOffset.UtcNow - window, maxPoints, ct);

    /// <summary>
    /// Shared query for latest and history: both directions of the market pair, newest first,
    /// capped by <paramref name="limit"/>. Dapper's async query API has no cancellation overload,
    /// so only connection open and the loop honor the token.
    /// </summary>
    private async Task<IReadOnlyList<ValuationPoint>> QueryPointsAsync(
        string itemId, string reference, string league, DateTimeOffset fromUtc, int limit, CancellationToken ct)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        // Column aliases match the record property names: Dapper does not strip underscores.
        var rows = await connection.QueryAsync<SnapshotRow>(
            """
            SELECT
                market_id AS MarketId,
                snapshot_hour_utc AS SnapshotHourUtc,
                metrics_json::text AS MetricsJson
            FROM exchange_rate_snapshots
            WHERE league = @League
              AND ((currency_a = @ItemId AND currency_b = @Reference)
                   OR (currency_a = @Reference AND currency_b = @ItemId))
              AND snapshot_hour_utc >= @FromUtc
            ORDER BY snapshot_hour_utc DESC
            LIMIT @Limit
            """,
            new
            {
                League = league,
                ItemId = itemId,
                Reference = reference,
                FromUtc = fromUtc,
                Limit = limit
            });

        var points = new List<ValuationPoint>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (TryToValuationPoint(row, itemId, reference) is { } point)
            {
                points.Add(point);
            }
        }

        return points;
    }

    private ValuationPoint? TryToValuationPoint(SnapshotRow row, string itemId, string reference)
    {
        MarketDigest digest;
        try
        {
            digest = JsonSerializer.Deserialize<MarketDigest>(row.MetricsJson, JsonOptions)
                ?? throw new JsonException("metrics_json deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping snapshot with unparseable metrics_json for market {MarketId}.", row.MarketId);
            return null;
        }

        return new ValuationPoint(
            row.MarketId,
            // Npgsql materializes a `timestamptz` column as System.DateTime (Kind=Utc), never as
            // DateTimeOffset, so Dapper can only materialize SnapshotRow when the record declares
            // DateTime. The instant is converted back here.
            ToUtcOffset(row.SnapshotHourUtc),
            ValuationRate.ReferencePerItem(digest.LowestRatio, itemId, reference),
            ValuationRate.ReferencePerItem(digest.HighestRatio, itemId, reference),
            digest.VolumeTraded.TryGetValue(itemId, out var itemVolume) ? itemVolume : null,
            digest.VolumeTraded.TryGetValue(reference, out var referenceVolume) ? referenceVolume : null);
    }

    /// <summary>Rebuilds the UTC offset for a <c>timestamptz</c> value read as a <see cref="DateTime"/>.</summary>
    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(), TimeSpan.Zero);

    private sealed record SnapshotRow(string MarketId, DateTime SnapshotHourUtc, string MetricsJson);
}

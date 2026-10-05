using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Data;

/// <summary>
/// Dapper-backed <see cref="IItemMetadataStore"/>: the item catalogue the catalog refresher writes
/// and the lookup endpoints read.
/// <para>
/// Lookups run against <c>lookup_key</c> (the normalized display name) so the comparison is
/// independent of the caller's spelling, and the market-partner query joins the catalogue to
/// <c>exchange_rate_snapshots</c> so a lookup can report which reference currencies actually have
/// data for the item.
/// </para>
/// </summary>
public sealed class PostgresItemMetadataStore : IItemMetadataStore
{
    /// <summary>Rows per insert statement during a catalogue swap; PostgreSQL allows far more, this is a payload cap.</summary>
    private const int InsertBatchSize = 200;

    /// <summary>Look-back window for "latest" market option; mirrors <c>PostgresMarketValuationStore</c>.</summary>
    private static readonly TimeSpan LatestLookback = TimeSpan.FromHours(24);

    /// <summary>Mirrors the poller's <c>JsonSerializer.Serialize(digest)</c> default options.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PostgresItemMetadataStore> _logger;

    public PostgresItemMetadataStore(IDbConnectionFactory connectionFactory, ILogger<PostgresItemMetadataStore> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ItemMetadataEntry>> SearchCandidatesAsync(
        string lookupKey, int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(lookupKey))
            return Array.Empty<ItemMetadataEntry>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        // lookup_key is the output of ItemNameNormalizer.Normalize, which only ever emits letters,
        // digits and single spaces — so it can never contain a LIKE wildcard and needs no escaping.
        var rows = await connection.QueryAsync<CatalogRow>(
            """
            SELECT metadata       AS Metadata,
                   display_name   AS DisplayName,
                   category       AS Category,
                   trade_alias    AS TradeAlias,
                   lookup_key     AS LookupKey
            FROM item_metadata
            WHERE lookup_key = @LookupKey
               OR lookup_key LIKE @LookupKey || '%'
               OR lookup_key LIKE '%' || @LookupKey || '%'
               OR COALESCE(trade_alias, '') = @LookupKey
            ORDER BY (lookup_key = @LookupKey) DESC, length(lookup_key), metadata
            LIMIT @Limit
            """,
            new { LookupKey = lookupKey, Limit = Math.Max(1, limit) });

        return rows
            .Select(r => new ItemMetadataEntry(r.Metadata, r.DisplayName, r.Category, r.TradeAlias, r.LookupKey))
            .ToList();
    }

    public async Task<IReadOnlyList<ItemMarketOption>> GetMarketOptionsAsync(
        string metadata, string league, int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(metadata) || string.IsNullOrWhiteSpace(league))
            return Array.Empty<ItemMarketOption>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        // One row per market partner: the newest snapshot of each pair within the look-back window.
        var rows = await connection.QueryAsync<MarketRow>(
            """
            WITH pairs AS (
                SELECT s.market_id,
                       s.snapshot_hour_utc,
                       s.metrics_json,
                       CASE WHEN s.currency_a = @Metadata THEN s.currency_b ELSE s.currency_a END AS reference
                FROM exchange_rate_snapshots s
                WHERE s.league = @League
                  AND @Metadata IN (s.currency_a, s.currency_b)
                  AND s.snapshot_hour_utc >= @FromUtc
            ),
            latest AS (
                SELECT DISTINCT ON (reference)
                       market_id, snapshot_hour_utc, metrics_json, reference
                FROM pairs
                ORDER BY reference, snapshot_hour_utc DESC
            )
            SELECT l.market_id                             AS MarketId,
                   l.reference                             AS Reference,
                   COALESCE(im.display_name, l.reference)  AS ReferenceDisplayName,
                   l.snapshot_hour_utc                     AS SnapshotHourUtc,
                   l.metrics_json                          AS MetricsJson
            FROM latest l
            LEFT JOIN item_metadata im ON im.metadata = l.reference
            ORDER BY im.display_name NULLS LAST, l.reference
            LIMIT @Limit
            """,
            new
            {
                Metadata = metadata,
                League = league,
                FromUtc = DateTimeOffset.UtcNow - LatestLookback,
                Limit = Math.Max(1, limit)
            });

        var options = new List<ItemMarketOption>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (ToMarketOption(row, metadata) is { } option)
                options.Add(option);
        }

        return options;
    }

    public async Task<int> ReplaceCatalogAsync(
        IReadOnlyList<ItemMetadataEntry> entries, DateTimeOffset refreshedAtUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            throw new ArgumentException("Item catalogue must not be empty.", nameof(entries));

        using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        // DELETE rather than TRUNCATE: a swap must not take an ACCESS EXCLUSIVE lock over the table,
        // because the lookup endpoints join it on every request.
        await using var transaction = (DbTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await connection.ExecuteAsync("DELETE FROM item_metadata", transaction: transaction);

            var written = 0;
            var refreshedAt = refreshedAtUtc.UtcDateTime;

            for (var offset = 0; offset < entries.Count; offset += InsertBatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = entries.Skip(offset).Take(InsertBatchSize).ToList();
                var (sql, parameters) = BuildInsert(batch, refreshedAt);
                written += await connection.ExecuteAsync(sql, parameters, transaction);
            }

            await transaction.CommitAsync(ct);
            return written;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<DateTimeOffset?> GetCatalogUpdatedAtAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        var updatedAt = await connection.QueryFirstOrDefaultAsync<DateTime?>(
            "SELECT max(refreshed_at_utc) FROM item_metadata");

        return updatedAt is { } value
            ? ToUtcOffset(value)
            : null;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(ct);

        return await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM item_metadata");
    }

    private static (string Sql, DynamicParameters Parameters) BuildInsert(
        IReadOnlyList<ItemMetadataEntry> batch, DateTime refreshedAtUtc)
    {
        var parameters = new DynamicParameters();
        var values = new string[batch.Count];

        for (var i = 0; i < batch.Count; i++)
        {
            var entry = batch[i];
            values[i] = $"(@metadata_{i}, @displayName_{i}, @category_{i}, @tradeAlias_{i}, @lookupKey_{i}, @refreshedAt_{i})";
            parameters.Add($"metadata_{i}", entry.Metadata);
            parameters.Add($"displayName_{i}", entry.DisplayName);
            parameters.Add($"category_{i}", entry.Category);
            parameters.Add($"tradeAlias_{i}", string.IsNullOrWhiteSpace(entry.TradeAlias) ? null : entry.TradeAlias);
            parameters.Add($"lookupKey_{i}", entry.LookupKey);
            parameters.Add($"refreshedAt_{i}", refreshedAtUtc);
        }

        var sql = $"""
            INSERT INTO item_metadata (metadata, display_name, category, trade_alias, lookup_key, refreshed_at_utc)
            VALUES {string.Join(", ", values)}
            ON CONFLICT (metadata) DO UPDATE SET
                display_name     = EXCLUDED.display_name,
                category         = EXCLUDED.category,
                trade_alias      = EXCLUDED.trade_alias,
                lookup_key       = EXCLUDED.lookup_key,
                refreshed_at_utc = EXCLUDED.refreshed_at_utc;
            """;

        return (sql, parameters);
    }

    private ItemMarketOption? ToMarketOption(MarketRow row, string itemId)
    {
        MarketDigest digest;
        try
        {
            digest = JsonSerializer.Deserialize<MarketDigest>(row.MetricsJson, JsonOptions)
                ?? throw new JsonException("metrics_json deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping market option with unparseable metrics_json for market {MarketId}.", row.MarketId);
            return null;
        }

        var reference = row.Reference;
        var (lowestRate, highestRate) = ValuationRate.ReferencePerItemRange(
            digest.LowestRatio, digest.HighestRatio, itemId, reference);

        return new ItemMarketOption(
            reference,
            row.ReferenceDisplayName,
            ToUtcOffset(row.SnapshotHourUtc),
            lowestRate,
            highestRate,
            digest.VolumeTraded.TryGetValue(itemId, out var itemVolume) ? itemVolume : null,
            digest.VolumeTraded.TryGetValue(reference, out var referenceVolume) ? referenceVolume : null);
    }

    /// <summary>Rebuilds the UTC offset for a <c>timestamptz</c> value, which Npgsql materializes as <see cref="DateTime"/> (never <see cref="DateTimeOffset"/>).</summary>
    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(), TimeSpan.Zero);

    private sealed record CatalogRow(string Metadata, string DisplayName, string Category, string? TradeAlias, string LookupKey);

    private sealed record MarketRow(
        string MarketId, string Reference, string ReferenceDisplayName, DateTime SnapshotHourUtc, string MetricsJson);
}




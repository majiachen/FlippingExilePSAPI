using System.Data.Common;
using Dapper;
using PoE.Valuation.Core.Data;

namespace PoE.Valuation.Infrastructure.Data;

/// <summary>
/// Dapper-based store for hourly currency-exchange snapshots and the polling cursor.
/// Connections come from <see cref="IDbConnectionFactory" /> and are opened per operation.
/// </summary>
public sealed class CurrencyExchangeStore : ICurrencyExchangeStore
{
    // Keep individual statements a sane size in memory; PostgreSQL allows up to 65535
    // parameters per statement, so this is a payload cap rather than a hard provider limit.
    private const int UpsertBatchSize = 300;

    private readonly IDbConnectionFactory _connectionFactory;

    public CurrencyExchangeStore(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<long?> ReadCursorAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        // Dapper 2.1.x async extensions have no CancellationToken overload; the token still
        // applies to connection open/close and transaction work below. The query must be
        // awaited here (not returned as a task) so the connection stays open for its lifetime.
        return await connection.QueryFirstOrDefaultAsync<long?>(
            "SELECT last_change_id FROM polling_state WHERE id = 1");
    }

    public async Task<bool> CommitHourAsync(
        IReadOnlyList<MarketSnapshotRow> rows,
        long? expectedCursor,
        long newCursor,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (DbTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (rows.Count > 0)
            {
                for (var offset = 0; offset < rows.Count; offset += UpsertBatchSize)
                {
                    var batch = rows.Skip(offset).Take(UpsertBatchSize).ToList();
                    var (sql, parameters) = BuildUpsert(batch);
                    await connection.ExecuteAsync(sql, parameters, transaction);
                }
            }

            var applied = await connection.ExecuteAsync(
                """
                UPDATE polling_state
                SET last_change_id = @newCursor,
                    updated_at_utc = now()
                WHERE id = 1
                  AND ((@expectedCursor IS NULL AND last_change_id IS NULL)
                       OR (@expectedCursor IS NOT NULL AND last_change_id = @expectedCursor))
                """,
                new { newCursor, expectedCursor },
                transaction);

            if (applied != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Builds a single upsert statement for <paramref name="batch"/> (batch size bounded by
    /// <see cref="UpsertBatchSize"/>). Rows with a duplicate (snapshot_hour_utc, league, market_id)
    /// key — e.g. a catch-up tick re-running a partially committed hour — are overwritten with the
    /// latest digest, so polling remains idempotent.
    /// </summary>
    private static (string Sql, DynamicParameters Parameters) BuildUpsert(IReadOnlyList<MarketSnapshotRow> batch)
    {
        var parameters = new DynamicParameters();
        var values = new string[batch.Count];

        for (var i = 0; i < batch.Count; i++)
        {
            var row = batch[i];
            // PostgreSQL does not implicitly cast text -> jsonb, so the digest payload must
            // be cast explicitly; this also validates the JSON at insert time.
            values[i] = $"(@hour_{i}, @league_{i}, @marketId_{i}, @currencyA_{i}, @currencyB_{i}, @metrics_{i}::jsonb)";
            parameters.Add($"hour_{i}", row.SnapshotHourUtc.UtcDateTime);
            parameters.Add($"league_{i}", row.League);
            parameters.Add($"marketId_{i}", row.MarketId);
            parameters.Add($"currencyA_{i}", row.CurrencyA);
            parameters.Add($"currencyB_{i}", row.CurrencyB);
            parameters.Add($"metrics_{i}", row.MetricsJson);
        }

        var sql = $"""
            INSERT INTO exchange_rate_snapshots (snapshot_hour_utc, league, market_id, currency_a, currency_b, metrics_json)
            VALUES {string.Join(", ", values)}
            ON CONFLICT (snapshot_hour_utc, league, market_id) DO UPDATE SET
                currency_a = EXCLUDED.currency_a,
                currency_b = EXCLUDED.currency_b,
                metrics_json = EXCLUDED.metrics_json;
            """;

        return (sql, parameters);
    }
}

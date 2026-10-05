using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Polling;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Polling;

/// <summary>
/// Background service that polls the public currency-exchange endpoint one completed hour at a time,
/// stores each digest in SQL and advances the persisted cursor. The poller is idempotent: it no-ops
/// when already caught up, seeds from the last completed hour on first run (no full-history backfill),
/// and caps catch-up work per tick so a long outage cannot stall the host for hours.
/// </summary>
public sealed class CurrencyExchangePoller : BackgroundService
{
    private readonly PoeCurrencyExchangeClient _client;
    private readonly ICurrencyExchangeStore _store;
    private readonly PollingOptions _options;
    private readonly ILogger<CurrencyExchangePoller> _logger;

    public CurrencyExchangePoller(
        PoeCurrencyExchangeClient client,
        ICurrencyExchangeStore store,
        IOptions<PollingOptions> options,
        ILogger<CurrencyExchangePoller> logger)
    {
        _client = client;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Currency exchange polling is disabled (Polling:Enabled=false); poller will not run.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPollCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Transient failures (network, SQL, 5xx) are logged and retried on the next tick.
                _logger.LogError(ex, "Currency exchange poll cycle failed; retrying in {IntervalSeconds}s.", _options.IntervalSeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>Runs one polling cycle: caught-up short-circuit, then a capped catch-up loop.</summary>
    public async Task RunPollCycleAsync(CancellationToken cancellationToken = default, DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var targetHourStart = CurrencyExchangeCursor.LastCompletedHourStart(now);

        var storedCursor = await _store.ReadCursorAsync(cancellationToken);

        // The commit's optimistic CAS must match the value actually read above — null on first
        // run. It must NOT match the seed value below, which does not exist in the database:
        // comparing against the seed would make the very first commit conflict with itself and
        // roll back forever.
        var expectedCursor = storedCursor;

        // First run: seed from the last completed hour instead of walking full history.
        var cursor = storedCursor ?? _options.InitialChangeIdUtc?.ToUnixTimeSeconds() ?? targetHourStart;
        if (CurrencyExchangeCursor.IsCaughtUp(cursor, now))
        {
            _logger.LogDebug("Currency exchange poller is caught up (cursor {Cursor}, last completed hour {LastCompleted}).",
                cursor, targetHourStart);
            return;
        }

        var fetched = 0;
        var nextId = cursor;
        while (nextId <= targetHourStart && fetched < _options.MaxCatchUpHoursPerTick)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _client.FetchDigestAsync(nextId, cancellationToken);
            if (result is not CurrencyExchangeResult.Ok ok)
            {
                // 404: the digest for this hour does not exist yet. Stop; retry on the next tick.
                _logger.LogInformation("Currency exchange digest {ChangeId} is not available yet; will retry on the next tick.", nextId);
                return;
            }

            // Only the tracked reference currencies are stored: a pair is kept when either side is
            // Chaos Orb or Divine Orb, so the snapshot table does not fill with every digest pair.
            var rows = ok.Digest.Markets
                .Where(m => TrackedCurrencyMetadata.IsTrackedPair(m.MarketPair.CurrencyA, m.MarketPair.CurrencyB))
                .Select(m => new MarketSnapshotRow(
                    DateTimeOffset.FromUnixTimeSeconds(nextId),
                    m.League,
                    m.MarketId,
                    m.MarketPair.CurrencyA,
                    m.MarketPair.CurrencyB,
                    JsonSerializer.Serialize(m)))
                .ToList();

            var committed = await _store.CommitHourAsync(rows, expectedCursor, ok.NextChangeId, cancellationToken);
            if (!committed)
            {
                // Another poller instance advanced the cursor first; stop to avoid double work.
                _logger.LogWarning("Cursor conflict at change id {ChangeId}; another poller committed this hour.", nextId);
                return;
            }

            cursor = ok.NextChangeId;
            expectedCursor = ok.NextChangeId; // The database now holds exactly what we just committed.
            nextId = ok.NextChangeId;
            fetched++;
        }

        if (fetched > 0)
            _logger.LogInformation("Currency exchange poller stored {Count} digest(s); cursor is now {Cursor}.", fetched, cursor);
    }
}

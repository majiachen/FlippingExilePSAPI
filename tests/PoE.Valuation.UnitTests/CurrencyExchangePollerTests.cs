using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Polling;
using PoE.Valuation.Infrastructure.Clients;
using PoE.Valuation.Infrastructure.Polling;

namespace PoE.Valuation.UnitTests;

/// <summary>
/// Regression tests for the first-run cursor seed. When no cursor is stored yet, the poller seeds
/// its in-memory cursor from <see cref="PollingOptions.InitialChangeIdUtc"/> (or the last completed
/// hour), but the commit's optimistic CAS must still compare against the value actually read from
/// the database — null on first run. Passing the seed value instead made the first commit conflict
/// with itself and roll back forever.
/// </summary>
public class CurrencyExchangePollerTests
{
    // "Now": 2026-09-27 12:30 UTC -> the last fully completed hour starts at 11:00 UTC.
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hour10 = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hour11 = new(2026, 9, 27, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hour12 = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FirstRun_WithNullStoredCursor_Commits_UsingNullAsExpectedCursor()
    {
        var (poller, handler, store) = CreatePoller(storedCursor: null, initialChangeIdUtc: Hour10);

        await poller.RunPollCycleAsync(CancellationToken.None, Now);

        // Two catch-up hours (10:00 and 11:00) were fetched and committed.
        Assert.Equal(2, handler.RequestCount);
        var commits = store.Commits;
        Assert.Equal(2, commits.Count);

        // The first commit must CAS against the stored value (null), not the InitialChangeIdUtc seed.
        Assert.Null(commits[0].Expected);
        Assert.Equal(Hour11.ToUnixTimeSeconds(), commits[0].New);
        Assert.Equal(2, commits[0].Rows);

        // After the first commit the database holds exactly what we committed, so the next
        // commit's CAS must use that value.
        Assert.Equal(Hour11.ToUnixTimeSeconds(), commits[1].Expected);
        Assert.Equal(Hour12.ToUnixTimeSeconds(), commits[1].New);
    }

    [Fact]
    public async Task WithStoredCursor_Commits_UsingStoredValueAsExpectedCursor()
    {
        var (poller, handler, store) = CreatePoller(
            storedCursor: Hour11.ToUnixTimeSeconds(), initialChangeIdUtc: Hour10);

        await poller.RunPollCycleAsync(CancellationToken.None, Now);

        var commit = Assert.Single(store.Commits);
        Assert.Equal(Hour11.ToUnixTimeSeconds(), commit.Expected);
        Assert.Equal(Hour12.ToUnixTimeSeconds(), commit.New);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CaughtUpCursor_FetchesNothing()
    {
        var (poller, handler, store) = CreatePoller(
            storedCursor: Hour12.ToUnixTimeSeconds(), initialChangeIdUtc: null);

        await poller.RunPollCycleAsync(CancellationToken.None, Now);

        Assert.Empty(store.Commits);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CursorConflict_StopsCatchUp_AfterSingleCommit()
    {
        var (poller, handler, store) = CreatePoller(
            storedCursor: Hour10.ToUnixTimeSeconds(), initialChangeIdUtc: Hour10, commitSucceeds: false);

        await poller.RunPollCycleAsync(CancellationToken.None, Now);

        var commit = Assert.Single(store.Commits);
        Assert.Equal(Hour10.ToUnixTimeSeconds(), commit.Expected);
        Assert.Equal(1, handler.RequestCount); // no second fetch after the conflict
    }

    private static (CurrencyExchangePoller Poller, FakeDigestHandler Handler, FakeExchangeStore Store) CreatePoller(
        long? storedCursor,
        DateTimeOffset? initialChangeIdUtc,
        bool commitSucceeds = true)
    {
        var store = new FakeExchangeStore { StoredCursor = storedCursor, CommitSucceeds = commitSucceeds };
        var handler = new FakeDigestHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://poe.test/currency-exchange/") };
        var options = Options.Create(new PollingOptions
        {
            Enabled = true,
            IntervalSeconds = 60,
            MaxCatchUpHoursPerTick = 24,
            InitialChangeIdUtc = initialChangeIdUtc
        });

        var poller = new CurrencyExchangePoller(
            new PoeCurrencyExchangeClient(http), store, options, NullLogger<CurrencyExchangePoller>.Instance);
        return (poller, handler, store);
    }

    private sealed record Commit(long? Expected, long New, int Rows);

    /// <summary>Stores each commit's CAS arguments and mirrors the database cursor on success.</summary>
    private sealed class FakeExchangeStore : ICurrencyExchangeStore
    {
        public long? StoredCursor { get; set; }
        public bool CommitSucceeds { get; init; } = true;
        public List<Commit> Commits { get; } = new();

        public Task<long?> ReadCursorAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(StoredCursor);

        public Task<bool> CommitHourAsync(
            IReadOnlyList<MarketSnapshotRow> rows,
            long? expectedCursor,
            long newCursor,
            CancellationToken cancellationToken = default)
        {
            Commits.Add(new Commit(expectedCursor, newCursor, rows.Count));
            if (CommitSucceeds)
                StoredCursor = newCursor;
            return Task.FromResult(CommitSucceeds);
        }
    }

    /// <summary>
    /// Answers every <c>GET {changeId}</c> with a two-market digest whose
    /// <c>next_change_id</c> is one hour after the requested id — mirroring the live API, where the
    /// same <c>market_id</c> exists in multiple leagues.
    /// </summary>
    private sealed class FakeDigestHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        private const string PayloadTemplate = """
            {"next_change_id": NEXT, "markets": [
              {"league":"Standard","market_id":"a|b","market_pair":["a","b"],"volume_traded":{},"lowest_stock":{},"highest_stock":{},"lowest_ratio":{},"highest_ratio":{}},
              {"league":"Hardcore","market_id":"a|b","market_pair":["a","b"],"volume_traded":{},"lowest_stock":{},"highest_stock":{},"lowest_ratio":{},"highest_ratio":{}}
            ]}
            """;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            // The client requests "{changeId}" relative to a base ending in ".../currency-exchange/",
            // so the requested hour is the final path segment.
            var changeId = long.Parse(request.RequestUri!.Segments.Last().Trim('/'));
            var json = PayloadTemplate.Replace("NEXT", (changeId + CurrencyExchangeCursor.HourSeconds).ToString());

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }
}

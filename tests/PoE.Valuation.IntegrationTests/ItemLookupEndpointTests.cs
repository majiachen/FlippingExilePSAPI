using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Redis;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Exercises the name → metadata-path map end to end: a stash-style item name resolves to the
/// metadata path the snapshots are keyed by, and the valuation endpoint accepts the name directly.
/// The session, rate limit, catalogue and snapshot stores are replaced with in-memory fakes, so the
/// test proves the routing, resolution and response shape without Redis, Postgres or the PoE API.
/// </summary>
public class ItemLookupEndpointTests
{
    private const string SessionId = "test-session-1";
    private const string Chaos = "Metadata/Items/Currency/CurrencyRerollRare";
    private const string Divine = "Metadata/Items/Currency/CurrencyModValues";

    [Theory]
    [InlineData("/api/items/lookup?q=Chaos+Orb")]
    [InlineData("/api/valuation/by-name/Chaos%20Orb")]
    public async Task NewGetEndpoints_WithoutSession_Return401(string path)
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore(), withSession: false);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CatalogRefresh_WithoutSession_Returns401()
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore(), withSession: false);

        using var response = await client.PostAsync("/api/items/catalog/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ItemLookup_ResolvesStashItemNameToMetadataPath()
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore());

        using var response = await client.GetAsync("/api/items/lookup?q=chaos%20orb&league=Standard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("chaos orb", root.GetProperty("query").GetString());
        Assert.Equal(1, root.GetProperty("count").GetInt32());

        var match = root.GetProperty("matches")[0];
        Assert.Equal(Chaos, match.GetProperty("itemId").GetString());
        Assert.Equal("Chaos Orb", match.GetProperty("displayName").GetString());
        Assert.Equal("Exact", match.GetProperty("matchKind").GetString());

        // The lookup also reports the reference currencies that actually have market data, which is
        // what a frontend needs before it can call the valuation endpoint.
        var market = match.GetProperty("markets")[0];
        Assert.Equal(Divine, market.GetProperty("reference").GetString());
        Assert.Equal("Divine Orb", market.GetProperty("referenceDisplayName").GetString());
        Assert.Equal(1.25, market.GetProperty("highestRate").GetDouble());
    }

    [Fact]
    public async Task ItemLookup_WithoutLeague_OmitsMarkets()
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore());

        using var response = await client.GetAsync("/api/items/lookup?q=Chaos+Orb");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(0, json.RootElement.GetProperty("matches")[0].GetProperty("markets").GetArrayLength());
    }

    [Fact]
    public async Task ItemLookup_BlankQuery_Returns400()
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore());

        using var response = await client.GetAsync("/api/items/lookup?q=");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValuationByName_ResolvesNameAndReturnsValuation()
    {
        var valuationStore = new FakeValuationStore();
        using var client = CreateClient(new FakeItemMetadataStore(), valuationStore);

        using var response = await client.GetAsync(
            "/api/valuation/by-name/Chaos%20Orb?reference=Divine%20Orb&league=Standard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Both sides reached the snapshot store as metadata paths, not as names.
        Assert.Equal((Chaos, Divine, "Standard"), valuationStore.LastQuery);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal(Chaos, root.GetProperty("item").GetProperty("itemId").GetString());
        Assert.Equal("Exact", root.GetProperty("item").GetProperty("matchKind").GetString());
        Assert.Equal(Divine, root.GetProperty("reference").GetProperty("itemId").GetString());
        Assert.Equal(0.8, root.GetProperty("lowestRate").GetDouble());
    }

    [Fact]
    public async Task ValuationByName_AcceptsMetadataPathWithoutCatalogueEntry()
    {
        var store = new FakeItemMetadataStore { Catalogue = Array.Empty<ItemMetadataEntry>() };
        using var client = CreateClient(store, new FakeValuationStore());

        using var response = await client.GetAsync(
            "/api/valuation/by-name/Metadata%2FItems%2FCurrency%2FCurrencyRerollRare" +
            "?reference=Metadata%2FItems%2FCurrency%2FCurrencyModValues&league=Standard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValuationByName_UnknownName_Returns404()
    {
        using var client = CreateClient(new FakeItemMetadataStore(), new FakeValuationStore());

        using var response = await client.GetAsync("/api/valuation/by-name/Not%20An%20Item");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Builds a client that carries the session cookie the /api surface requires.</summary>
    private static HttpClient CreateClient(IItemMetadataStore metadataStore, IMarketValuationStore valuationStore, bool withSession = true)
    {
        var factory = ValuationApiFactory.CreateWithServices(services =>
        {
            services.AddSingleton<ISessionStore>(new FakeSessionStore());
            services.AddSingleton<IRateLimiter>(new AllowAllRateLimiter());
            services.AddSingleton(metadataStore);
            services.AddSingleton(valuationStore);
        });

        var client = factory.CreateClient();
        if (withSession)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"poe_sid={SessionId}");
        }
        return client;
    }



    private sealed class FakeItemMetadataStore : IItemMetadataStore
    {
        public IReadOnlyList<ItemMetadataEntry> Catalogue { get; set; } = new[]
        {
            Entry(Chaos, "Chaos Orb", "chaos"),
            Entry(Divine, "Divine Orb", "divine"),
        };

        public Task<IReadOnlyList<ItemMetadataEntry>> SearchCandidatesAsync(
            string lookupKey, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ItemMetadataEntry>>(
                Catalogue.Where(e => e.LookupKey.Contains(lookupKey, StringComparison.Ordinal)).ToList());

        public Task<IReadOnlyList<ItemMarketOption>> GetMarketOptionsAsync(
            string metadata, string league, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ItemMarketOption>>(new[]
            {
                new ItemMarketOption(
                    Divine, "Divine Orb",
                    new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
                    LowestRate: 1.1, HighestRate: 1.25, VolumeItem: 40, VolumeReference: 50),
            });

        public Task<int> ReplaceCatalogAsync(
            IReadOnlyList<ItemMetadataEntry> entries, DateTimeOffset refreshedAtUtc, CancellationToken ct = default) =>
            Task.FromResult(entries.Count);

        public Task<DateTimeOffset?> GetCatalogUpdatedAtAsync(CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero));

        public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(Catalogue.Count);

        private static ItemMetadataEntry Entry(string metadata, string name, string alias) =>
            new(metadata, name, "Currency", alias,
                PoE.Valuation.Core.Validation.ItemNameNormalizer.Normalize(name));
    }

    private sealed class FakeValuationStore : IMarketValuationStore
    {
        public (string ItemId, string Reference, string League)? LastQuery { get; private set; }

        public Task<ValuationPoint?> GetLatestAsync(string itemId, string reference, string league, CancellationToken ct = default)
        {
            LastQuery = (itemId, reference, league);
            if (itemId != Chaos || reference != Divine)
                return Task.FromResult<ValuationPoint?>(null);

            return Task.FromResult<ValuationPoint?>(new ValuationPoint(
                $"{itemId}|{reference}",
                new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
                LowestRate: 0.8, HighestRate: 1.25, VolumeItem: 40, VolumeReference: 50));
        }

        public Task<IReadOnlyList<ValuationPoint>> GetHistoryAsync(
            string itemId, string reference, string league, TimeSpan window, int maxPoints, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ValuationPoint>>(Array.Empty<ValuationPoint>());
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        // Expiry far enough out that SessionService returns the entry without attempting a refresh.
        private static readonly SessionEntry Entry = new(
            "access-token", "refresh-token",
            DateTimeOffset.UtcNow.AddDays(1), "sub-1", "tester", DateTimeOffset.UtcNow);

        public Task<SessionEntry?> GetAsync(string sessionId, CancellationToken ct = default) =>
            Task.FromResult(sessionId == SessionId ? Entry : (SessionEntry?)null);

        public Task SetAsync(string sessionId, SessionEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> DeleteAsync(string sessionId, CancellationToken ct = default) => Task.FromResult(true);

        public Task ReplaceAsync(string sessionId, SessionEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AllowAllRateLimiter : IRateLimiter
    {
        public Task<bool> TryAcquireAsync(
            string sessionId, string endpoint, int maxRequests, TimeSpan window, CancellationToken ct = default) =>
            Task.FromResult(true);
    }
}



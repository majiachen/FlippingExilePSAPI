using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Core.Validation;
using PoE.Valuation.Contracts.Valuation;
using PoE.Valuation.Infrastructure.Clients;
using PoE.Valuation.Infrastructure.Services;

namespace PoE.Valuation.Api.Endpoints;

/// <summary>
/// The authenticated <c>/api</c> surface: latest valuation, price history, item-name → metadata-path
/// lookup, valuation by item name, and the user's stash (tech doc, sections 6 &amp; 7). Every route
/// requires a valid <see cref="SessionCookie"/> session and is rate-limited per session + endpoint.
/// The reference item and league are supplied by the client and default to empty; they are only
/// validated when non-empty, so an empty reference or league simply yields no matching market
/// (valuation → 404, history → empty series).
/// <para>
/// The valuation and history routes take a metadata path. The <c>/api/items/lookup</c> and
/// <c>/api/valuation/by-name/{itemName}</c> routes exist because the stash API only ever publishes an
/// item's <c>name</c>/<c>base_type</c>, never its metadata path — they bridge that gap.
/// </para>
/// </summary>

public static class ApiEndpoints
{
    /// <summary>Matches a lookup returns when the client does not ask for a specific number.</summary>
    private const int DefaultLookupLimit = 10;

    /// <summary>Upper bound on <c>limit</c> so one lookup cannot fan out into an unbounded snapshot scan.</summary>
    private const int MaxLookupLimit = 50;

    /// <summary>Candidates fetched per returned match, before <see cref="ItemNameMatcher.Rank"/> trims to <c>limit</c>.</summary>
    private const int CandidateOverFetch = 4;

    /// <summary>Candidates considered when resolving a single name for the by-name valuation.</summary>
    private const int LookupCandidateLimit = 20;

    /// <summary>Reference currencies reported per match by the lookup endpoint.</summary>
    private const int MaxMarketOptions = 20;

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder endpoints)

    {
        endpoints.MapGet("/api/valuation/{itemId}", async (
            HttpContext ctx, string itemId, string? reference, string? league,
            SessionService sessionService, IMarketValuationStore store,
            IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions) =>
        {
            if (await EnsureAuthorizedAsync(ctx, sessionService, rateLimiter, rateOptions.Value, "valuation") is { } rejected)
                return rejected;

            itemId = DecodeRouteValue(itemId);

            if (!ApiInputValidator.TryValidateItemId(itemId, out var error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(reference) && !ApiInputValidator.TryValidateItemId(reference, out error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(league) && !ApiInputValidator.TryValidateLeagueName(league, out error))
                return BadInput(error);

            var point = await store.GetLatestAsync(itemId, reference ?? string.Empty, league ?? string.Empty);
            if (point is null)
                return Results.NotFound();

            return Results.Ok(new ValuationResponse(
                itemId, reference ?? string.Empty, league ?? string.Empty, point.MarketId,
                point.SnapshotHourUtc, point.LowestRate, point.HighestRate, point.VolumeItem, point.VolumeReference));
        });

        endpoints.MapGet("/api/history/{itemId}", async (
            HttpContext ctx, string itemId, string? reference, string? league, string? range,
            SessionService sessionService, IMarketValuationStore store,
            IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions) =>
        {
            if (await EnsureAuthorizedAsync(ctx, sessionService, rateLimiter, rateOptions.Value, "history") is { } rejected)
                return rejected;

            if (!ApiInputValidator.TryParseHistoryRange(range, out var window, out var maxPoints, out var error))
                return BadInput(error);

            itemId = DecodeRouteValue(itemId);

            if (!ApiInputValidator.TryValidateItemId(itemId, out error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(reference) && !ApiInputValidator.TryValidateItemId(reference, out error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(league) && !ApiInputValidator.TryValidateLeagueName(league, out error))
                return BadInput(error);

            var points = await store.GetHistoryAsync(itemId, reference ?? string.Empty, league ?? string.Empty, window, maxPoints);
            return Results.Ok(new HistoryResponse(
                itemId,
                reference ?? string.Empty,
                league ?? string.Empty,
                range!.Trim().ToLowerInvariant(),
                DateTimeOffset.UtcNow - window,
                points.Count,
                points.Select(p => new HistoryPointDto(p.SnapshotHourUtc, p.LowestRate, p.HighestRate, p.VolumeItem, p.VolumeReference)).ToList(),
                false));
        });

        endpoints.MapGet("/api/items/lookup", async (
            HttpContext ctx, string? q, string? league, int? limit,
            SessionService sessionService, IItemMetadataStore metadataStore,
            IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions) =>
        {
            if (await EnsureAuthorizedAsync(ctx, sessionService, rateLimiter, rateOptions.Value, "items-lookup") is { } rejected)
                return rejected;

            if (!ApiInputValidator.TryValidateItemName(q, out var error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(league) && !ApiInputValidator.TryValidateLeagueName(league, out error))
                return BadInput(error);

            var take = Math.Clamp(limit ?? DefaultLookupLimit, 1, MaxLookupLimit);

            // The store filters generously and ItemNameMatcher.Rank does the ranking and the
            // truncation, so a candidate the store ordered late can still rank first.
            var candidates = await metadataStore.SearchCandidatesAsync(
                ItemNameNormalizer.Normalize(q!), take * CandidateOverFetch);
            var matches = ItemNameMatcher.Rank(q!, candidates, take);

            var resolved = new List<ItemMatchDto>(matches.Count);
            foreach (var match in matches)
            {
                // Markets are only reported when a league is supplied: without one there is nothing
                // to scope the snapshot query to.
                IReadOnlyList<ItemMarketOptionDto> markets = string.IsNullOrEmpty(league)
                    ? Array.Empty<ItemMarketOptionDto>()
                    : (await metadataStore.GetMarketOptionsAsync(match.Metadata, league!, MaxMarketOptions))
                        .Select(ToMarketOptionDto).ToList();

                resolved.Add(new ItemMatchDto(
                    match.Metadata, match.DisplayName, match.Category, match.TradeAlias,
                    match.Kind.ToString(), markets));
            }

            return Results.Ok(new ItemLookupResponse(
                q!, await metadataStore.GetCatalogUpdatedAtAsync(), resolved.Count, resolved));
        });

        endpoints.MapGet("/api/valuation/by-name/{itemName}", async (
            HttpContext ctx, string itemName, string? reference, string? league,
            SessionService sessionService, IItemMetadataStore metadataStore, IMarketValuationStore store,
            IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions) =>
        {
            if (await EnsureAuthorizedAsync(ctx, sessionService, rateLimiter, rateOptions.Value, "valuation-by-name") is { } rejected)
                return rejected;

            itemName = DecodeRouteValue(itemName);

            if (!ApiInputValidator.TryValidateItemName(itemName, out var error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(reference) && !ApiInputValidator.TryValidateItemName(reference, out error))
                return BadInput(error);
            if (!string.IsNullOrEmpty(league) && !ApiInputValidator.TryValidateLeagueName(league, out error))
                return BadInput(error);

            var itemMatches = await ResolveRankedAsync(metadataStore, itemName);
            if (itemMatches.Count == 0)
                return NoMatch(itemName);

            // The reference may be a metadata path (the usual case) or another item name.
            var referenceMatches = string.IsNullOrEmpty(reference)
                ? Array.Empty<ItemMetadataMatch>()
                : await ResolveRankedAsync(metadataStore, reference!);
            if (!string.IsNullOrEmpty(reference) && referenceMatches.Count == 0)
                return NoMatch(reference!);

            var point = await store.GetLatestAsync(
                itemMatches[0].Metadata,
                referenceMatches.Count > 0 ? referenceMatches[0].Metadata : string.Empty,
                league ?? string.Empty);
            if (point is null)
                return Results.NotFound();

            return Results.Ok(new ItemValuationResponse(
                ToResolvedDto(itemName, itemMatches),
                ToResolvedDto(reference ?? string.Empty, referenceMatches),
                league ?? string.Empty,
                point.MarketId,
                point.SnapshotHourUtc,
                point.LowestRate, point.HighestRate, point.VolumeItem, point.VolumeReference));
        });

        endpoints.MapPost("/api/items/catalog/refresh", async (
            HttpContext ctx, ItemMetadataCatalogService catalog, IItemMetadataStore metadataStore,
            SessionService sessionService, IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions) =>
        {
            if (await EnsureAuthorizedAsync(ctx, sessionService, rateLimiter, rateOptions.Value, "items-catalog-refresh") is { } rejected)
                return rejected;

            var written = await catalog.RefreshAsync(ctx.RequestAborted);
            return Results.Ok(new ItemCatalogRefreshResponse(
                written,
                await metadataStore.CountAsync(ctx.RequestAborted),
                await metadataStore.GetCatalogUpdatedAtAsync(ctx.RequestAborted)));
        });

        endpoints.MapGet("/api/stash", async (

            HttpContext ctx, string? league,
            SessionService sessionService, IStashCache stashCache,
            IRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions,
            PoeStashClient poeClient, LeagueIdResolver leagueIdResolver) =>
        {
            var sessionId = SessionCookie.GetSessionId(ctx.Request);
            if (sessionId is null)
                return Results.Unauthorized();
            var session = await sessionService.GetUserSessionAsync(sessionId);
            if (session is null)
                return Results.Unauthorized();

            if (!await rateLimiter.TryAcquireAsync(sessionId, "stash", rateOptions.Value.DefaultMaxRequests, RateLimitWindow(rateOptions.Value)))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            if (string.IsNullOrWhiteSpace(league))
                return BadInput("League is required.");
            if (!ApiInputValidator.TryValidateLeagueName(league, out var error))
                return BadInput(error);

            var leagueId = await leagueIdResolver.ResolveAsync(league);
            if (leagueId is null)
                return BadInput($"League '{league}' was not found.");

            var cached = await stashCache.GetAsync(sessionId);
            if (cached is not null && string.Equals(cached.LeagueId, leagueId, StringComparison.Ordinal))
                return Results.Ok(ToStashResponse(league, leagueId, cached, cached: true));

            var items = await poeClient.GetStashAsync(leagueId, session.AccessToken);
            var entry = new StashEntry(
                items.Select(i => ToStashItem(i, league)).ToList(),
                DateTimeOffset.UtcNow,
                leagueId);
            await stashCache.SetAsync(sessionId, entry);
            return Results.Ok(ToStashResponse(league, leagueId, entry, cached: false));

        });

        return endpoints;
    }

    /// <summary>Resolves + verifies the caller's session, then applies the endpoint rate limit.</summary>
    private static async Task<IResult?> EnsureAuthorizedAsync(
        HttpContext ctx, SessionService sessionService, IRateLimiter rateLimiter,
        RateLimitOptions rateOptions, string endpoint)
    {
        var sessionId = SessionCookie.GetSessionId(ctx.Request);
        if (sessionId is null)
            return Results.Unauthorized();

        if (await sessionService.GetUserSessionAsync(sessionId) is null)
            return Results.Unauthorized();

        if (!await rateLimiter.TryAcquireAsync(sessionId, endpoint, rateOptions.DefaultMaxRequests, RateLimitWindow(rateOptions)))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        return null;
    }

    private static TimeSpan RateLimitWindow(RateLimitOptions options) =>
        TimeSpan.FromSeconds(Math.Max(1, options.DefaultWindowSeconds));

    /// <summary>
    /// Normalizes an item id taken from a route segment. Kestrel leaves <c>%2F</c> encoded in the
    /// matched path, so a PoE metadata path (<c>Metadata/Items/Currency/...</c>) passed as
    /// <c>{itemId}</c> arrives percent-encoded while the same value in a query string arrives
    /// decoded. Both must reach the store in the decoded form the snapshots are keyed by.
    /// </summary>
    private static string DecodeRouteValue(string value) => Uri.UnescapeDataString(value);

    /// <summary>
    /// Resolves one client-supplied item reference to ranked catalogue matches. A value that is
    /// already a metadata path (<c>Metadata/…</c>) is accepted even when the catalogue has no entry
    /// for it — the snapshot lookup is what decides whether data exists for it.
    /// </summary>
    private static async Task<IReadOnlyList<ItemMetadataMatch>> ResolveRankedAsync(
        IItemMetadataStore metadataStore, string input)
    {
        var candidates = await metadataStore.SearchCandidatesAsync(
            ItemNameNormalizer.Normalize(input), LookupCandidateLimit);
        var ranked = ItemNameMatcher.Rank(input, candidates, LookupCandidateLimit);

        if (ranked.Count == 0 && ItemNameMatcher.IsMetadataPath(input))
        {
            var metadata = input.Trim();
            return new[] { new ItemMetadataMatch(metadata, metadata, string.Empty, null, ItemMatchKind.Exact) };
        }

        return ranked;
    }

    private static ResolvedItemDto ToResolvedDto(string input, IReadOnlyList<ItemMetadataMatch> matches) =>
        matches.Count == 0
            ? new ResolvedItemDto(input, string.Empty, string.Empty, ItemMatchKind.None.ToString(), Array.Empty<string>())
            : new ResolvedItemDto(
                input,
                matches[0].Metadata,
                matches[0].DisplayName,
                matches[0].Kind.ToString(),
                matches.Skip(1).Select(m => m.Metadata).ToList());

    private static ItemMarketOptionDto ToMarketOptionDto(ItemMarketOption option) =>
        new(option.Reference, option.ReferenceDisplayName, option.SnapshotHourUtc,
            option.LowestRate, option.HighestRate, option.VolumeItem, option.VolumeReference);

    private static IResult BadInput(string? error) =>
        Results.Problem(title: "Invalid input", detail: error, statusCode: 400);

    private static IResult NoMatch(string input) =>
        Results.Problem(
            title: "No catalogue match",
            detail: $"No item in the PoE item catalogue matches '{input}'. Try GET /api/items/lookup?q={Uri.EscapeDataString(input)}.",
            statusCode: StatusCodes.Status404NotFound);


    private static StashItem ToStashItem(StashApiItem item, string league) => new(
        item.ItemId, league, item.Name, item.BaseType, item.FrameType, item.ItemLevel,
        item.StackSize, item.GemLevel, item.GemQuality, item.Corrupted, item.Verified, item.Note);

    private static StashResponse ToStashResponse(string league, string leagueId, StashEntry entry, bool cached) =>

        new(
            league,
            leagueId,
            entry.LastRefreshedAt,
            entry.Items.Count,
            entry.Items
                .Select(i => new StashItemDto(i.ItemId, i.League, i.Name, i.BaseType, i.FrameType, i.ItemLevel, i.StackSize, i.GemLevel, i.GemQuality, i.Corrupted, i.Verified, i.Note))
                .ToList(),
            cached);
}
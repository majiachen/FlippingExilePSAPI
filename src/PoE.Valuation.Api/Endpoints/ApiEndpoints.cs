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
/// The authenticated <c>/api</c> surface: latest valuation, price history and the user's stash
/// (tech doc, sections 6 &amp; 7). Every route requires a valid <see cref="SessionCookie"/> session
/// and is rate-limited per session + endpoint. The reference item and league are supplied by the
/// client and default to empty; they are only validated when non-empty, so an empty reference or
/// league simply yields no matching market (valuation → 404, history → empty series).
/// </summary>
public static class ApiEndpoints
{
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
            if (cached is not null && cached.LeagueId == leagueId)
                return Results.Ok(ToStashResponse(league, leagueId.Value, cached, cached: true));

            var items = await poeClient.GetStashAsync(leagueId.Value, session.AccessToken);
            var entry = new StashEntry(
                items.Select(i => ToStashItem(i, league)).ToList(),
                DateTimeOffset.UtcNow,
                leagueId.Value);
            await stashCache.SetAsync(sessionId, entry);
            return Results.Ok(ToStashResponse(league, leagueId.Value, entry, cached: false));
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

    private static IResult BadInput(string? error) =>
        Results.Problem(title: "Invalid input", detail: error, statusCode: 400);

    private static StashItem ToStashItem(StashApiItem item, string league) => new(
        item.ItemId, league, item.Name, item.BaseType, item.FrameType, item.ItemLevel,
        item.StackSize, item.GemLevel, item.GemQuality, item.Corrupted, item.Verified, item.Note);

    private static StashResponse ToStashResponse(string league, int leagueId, StashEntry entry, bool cached) =>
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
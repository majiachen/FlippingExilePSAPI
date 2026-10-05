using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Services;

/// <summary>
/// Resolves a league <em>name</em> (the representation the DB and the public API use) to the league
/// <em>id</em> the PoE stash endpoint requires. PoE publishes the id as the league name itself
/// (<c>"id": "Standard"</c>), so the mapping is usually the identity — but it is still resolved
/// through <c>GET /leagues</c> so an unknown league is a 400 rather than a 404 from PoE. The lookup
/// is cached in Redis under <c>league:{name}</c> for 24 hours so steady state costs one Redis read
/// per cache miss rather than a PoE API call.
/// </summary>
public sealed class LeagueIdResolver
{
    /// <summary>League lists change at most once per league start; 24 h of cache is ample.</summary>
    private static readonly TimeSpan LeagueCacheTtl = TimeSpan.FromHours(24);

    private readonly PoeStashClient _poeClient;
    private readonly IRedisStore _redis;
    private readonly ILogger<LeagueIdResolver> _logger;

    public LeagueIdResolver(PoeStashClient poeClient, IRedisStore redis, ILogger<LeagueIdResolver> logger)
    {
        _poeClient = poeClient ?? throw new ArgumentNullException(nameof(poeClient));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the league id for <paramref name="leagueName"/>, or null when the league is unknown.
    /// Throws <see cref="RedisConnectionException"/> when Redis is down (→ 503) and
    /// <see cref="HttpRequestException"/>/JSON errors when the PoE API is unreachable (→ 502).
    /// </summary>
    public async Task<string?> ResolveAsync(string leagueName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leagueName);

        var key = RedisKeys.LeagueMapping(leagueName);
        if (await _redis.GetAsync<string>(key, ct) is { Length: > 0 } cachedId)
        {
            return cachedId;
        }

        var leagues = await _poeClient.GetLeaguesAsync(ct);
        var leagueId = leagues
            .FirstOrDefault(l => string.Equals(l.Name, leagueName, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(l.Id, leagueName, StringComparison.OrdinalIgnoreCase))
            ?.Id;

        if (string.IsNullOrWhiteSpace(leagueId))
        {
            _logger.LogDebug("League {LeagueName} not found in the PoE league list.", leagueName);
            return null;
        }

        await _redis.SetAsync(key, leagueId, LeagueCacheTtl, ct);
        return leagueId;
    }
}


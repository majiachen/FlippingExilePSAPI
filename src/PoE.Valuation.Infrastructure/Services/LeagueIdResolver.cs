using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Services;

/// <summary>
/// Resolves a league <em>name</em> (the representation the DB and the public API use) to the
/// numeric league <em>id</em> the PoE stash endpoint requires. The <c>GET /leagues</c> lookup is
/// cached in Redis under <c>league:{name}</c> for 24 hours so steady state costs one Redis read
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
    /// Returns the numeric league id for <paramref name="leagueName"/>, or null when the league is
    /// unknown. Throws <see cref="RedisConnectionException"/> when Redis is down (→ 503) and
    /// <see cref="HttpRequestException"/>/JSON errors when the PoE API is unreachable (→ 502).
    /// </summary>
    public async Task<int?> ResolveAsync(string leagueName, CancellationToken ct = default)
    {
        var key = RedisKeys.LeagueMapping(leagueName);
        var cached = await _redis.GetAsync<int?>(key, ct);
        if (cached is { } cachedId)
        {
            return cachedId;
        }

        var leagues = await _poeClient.GetLeaguesAsync(ct);
        var leagueId = leagues
            .FirstOrDefault(l => string.Equals(l.Name, leagueName, StringComparison.OrdinalIgnoreCase))
            ?.Id;

        if (leagueId is null)
        {
            _logger.LogDebug("League {LeagueName} not found in the PoE league list.", leagueName);
            return null;
        }

        await _redis.SetAsync(key, leagueId.Value, LeagueCacheTtl, ct);
        return leagueId;
    }
}

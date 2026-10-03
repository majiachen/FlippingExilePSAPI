namespace PoE.Valuation.Core.Configuration;

/// <summary>
/// Redis connection and TTL settings. Bound from the "Redis" configuration section.
/// The connection string is a standard StackExchange.Redis connection string.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public const string ConnectionStringKey = "ConnectionString";

    /// <summary>StackExchange.Redis connection string (sessions, caches, service tokens, locks and rate limits).</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>TTL for user sessions (<c>session:{sessionId}</c>) in days.</summary>
    public int SessionTtlDays { get; init; } = 90;

    /// <summary>TTL for stash caches (<c>stash:{sessionId}</c>) in minutes.</summary>
    public int StashTtlMinutes { get; init; } = 15;

    /// <summary>TTL for history caches (<c>history:{itemId}:{range}</c>) in minutes.</summary>
    public int HistoryCacheTtlMinutes { get; init; } = 5;

    /// <summary>TTL for OAuth state + PKCE verifiers (<c>oauth:state:{state}</c>) in minutes.</summary>
    public int OAuthStateTtlMinutes { get; init; } = 10;

    /// <summary>TTL for the polling distributed lock (<c>lock:polling:{leagueId}</c>) in minutes; renewed automatically while held.</summary>
    public int PollingLockTtlMinutes { get; init; } = 5;

    /// <summary>Window of the rate-limit counters (<c>ratelimit:{sessionId}:{endpoint}</c>) in seconds.</summary>
    public int RateLimitWindowSeconds { get; init; } = 60;

    public TimeSpan SessionTtl => TimeSpan.FromDays(SessionTtlDays);
    public TimeSpan StashTtl => TimeSpan.FromMinutes(StashTtlMinutes);
    public TimeSpan HistoryCacheTtl => TimeSpan.FromMinutes(HistoryCacheTtlMinutes);
    public TimeSpan OAuthStateTtl => TimeSpan.FromMinutes(OAuthStateTtlMinutes);
    public TimeSpan PollingLockTtl => TimeSpan.FromMinutes(PollingLockTtlMinutes);
    public TimeSpan RateLimitWindow => TimeSpan.FromSeconds(RateLimitWindowSeconds);
}
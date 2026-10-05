using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Clients;
using PoE.Valuation.Infrastructure.Data;
using PoE.Valuation.Infrastructure.Polling;
using PoE.Valuation.Infrastructure.Redis;
using PoE.Valuation.Infrastructure.Services;
using StackExchange.Redis;

namespace PoE.Valuation.Infrastructure.DependencyInjection;

/// <summary>Registration of infrastructure services (SQL, Redis, currency-exchange polling).</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Binds the Redis/SQL/PoE/polling options and registers lazily-connecting clients.
    /// Neither resource is touched at startup: connections are established on first use.
    /// </summary>
    public static IServiceCollection AddValuationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<SqlOptions>(configuration.GetSection(SqlOptions.SectionName));
        services.Configure<PoEApiOptions>(configuration.GetSection(PoEApiOptions.SectionName));
        services.Configure<PollingOptions>(configuration.GetSection(PollingOptions.SectionName));

        services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
        services.AddSingleton<ICurrencyExchangeStore, CurrencyExchangeStore>();
        services.AddSingleton<IMarketValuationStore, PostgresMarketValuationStore>();
        services.AddSingleton<IItemMetadataStore, PostgresItemMetadataStore>();


        // Redis: one long-lived ConnectionMultiplexer for the whole app — never one per request.
        // AbortOnConnectFail=false is required: when Redis is temporarily unavailable the
        // multiplexer keeps reconnecting in the background instead of tearing the app down
        // (tech doc, 2.2). The factory still runs only on first resolution, so startup needs
        // no live Redis.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configOptions = ConfigurationOptions.Parse(sp.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectTimeout = 5_000;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // The Redis layer (tech doc, sections 2, 4, 5 and 8): one low-level store plus the
        // higher-level stores built on top of it.
        services.AddSingleton<IRedisStore, RedisStore>();
        services.AddSingleton<IServiceTokenProvider, RedisServiceTokenProvider>();
        services.AddSingleton<IDistributedLock, RedisDistributedLock>();
        services.AddSingleton<ISessionStore, RedisSessionStore>();
        services.AddSingleton<IStashCache, RedisStashCache>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        // Typed HttpClient for the public currency-exchange endpoint. The base address ends with a slash so
        // relative request URIs (the hourly change id) resolve against it; user agent and timeout come from PoEApiOptions.
        services.AddHttpClient<PoeCurrencyExchangeClient>((sp, client) =>
        {
            var poeApi = sp.GetRequiredService<IOptions<PoEApiOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(poeApi.CurrencyExchangeBaseUrl, nameof(PoEApiOptions.CurrencyExchangeBaseUrl)));
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, poeApi.RequestTimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(poeApi.UserAgent))
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", poeApi.UserAgent);
        });

        // Typed HttpClient for the PoE REST API (league list + stash). Base address is PathOfExile:ApiBaseUrl.
        services.AddHttpClient<PoeStashClient>((sp, client) =>
        {
            var poeApi = sp.GetRequiredService<IOptions<PoEApiOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(poeApi.ApiBaseUrl, nameof(PoEApiOptions.ApiBaseUrl)));
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, poeApi.RequestTimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(poeApi.UserAgent))
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", poeApi.UserAgent);
        });

        // Typed HttpClient for the public item catalogue (name -> metadata path). Base address is
        // PathOfExile:TradeDataBaseUrl; the client requests the relative "static" path.
        services.AddHttpClient<PoeItemCatalogClient>((sp, client) =>
        {
            var poeApi = sp.GetRequiredService<IOptions<PoEApiOptions>>().Value;
            client.BaseAddress = new Uri(EnsureTrailingSlash(poeApi.TradeDataBaseUrl, nameof(PoEApiOptions.TradeDataBaseUrl)));
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, poeApi.RequestTimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(poeApi.UserAgent))
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", poeApi.UserAgent);
        });


        // Typed HttpClient for the OAuth token exchange. The token endpoint is an absolute URL
        // (OAuth:TokenEndpoint) passed per call, so no base address is set.
        services.AddHttpClient<PoeOAuthClient>((sp, client) =>
        {
            var poeApi = sp.GetRequiredService<IOptions<PoEApiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, poeApi.RequestTimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(poeApi.UserAgent))
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", poeApi.UserAgent);
        });

        // League name → league id resolution, cached in Redis (see LeagueIdResolver).
        services.AddSingleton<LeagueIdResolver>();

        // Item catalogue (item name -> metadata path) refresh, guarded by the Redis refresh lock.
        services.AddSingleton<ItemMetadataCatalogService>();


        // Session/auth orchestration (login, callback, logout, transparent token refresh). Scoped
        // because it depends on the transient PoeOAuthClient typed HTTP client.
        services.AddScoped<SessionService>();

        // Background poller; no-ops when Polling:Enabled=false.
        services.AddHostedService<CurrencyExchangePoller>();

        // Item catalogue refresher; no-ops when Polling:ItemCatalogEnabled=false.
        services.AddHostedService<ItemMetadataCatalogRefresher>();

        return services;
    }

    private static string EnsureTrailingSlash(string baseUrl, string settingName) =>
        string.IsNullOrWhiteSpace(baseUrl)
            ? throw new InvalidOperationException($"{PoEApiOptions.SectionName}:{settingName} is empty.")
            : baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
}


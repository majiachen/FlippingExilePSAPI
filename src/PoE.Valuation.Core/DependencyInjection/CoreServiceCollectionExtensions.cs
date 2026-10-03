using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PoE.Valuation.Core.Configuration;

namespace PoE.Valuation.Core.DependencyInjection;

/// <summary>Registers core valuation services: strongly-typed options bound from configuration.</summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>Binds all option classes from their configuration sections.</summary>
    public static IServiceCollection AddValuationCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OAuthOptions>(configuration.GetSection(OAuthOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<SqlOptions>(configuration.GetSection(SqlOptions.SectionName));
        services.Configure<PoEApiOptions>(configuration.GetSection(PoEApiOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        return services;
    }
}

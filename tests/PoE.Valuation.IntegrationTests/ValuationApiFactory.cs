using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Boots the real API host with in-memory configuration. Redis and SQL are never
/// touched at startup (lazy connections), so no external services are required.
/// The currency-exchange poller is disabled so tests never hit live PoE endpoints.
/// </summary>
public sealed class ValuationApiFactory : WebApplicationFactory<Program>
{
    public ValuationApiFactory() =>
        WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    /// <summary>Creates a factory for tests that manage the host lifetime explicitly (xunit disallows static members on IClassFixture types, so fixture-based tests use the constructor instead).</summary>
    public static ValuationApiFactory Create() => new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:ClientId"] = "test-client-id",
                ["OAuth:ClientSecret"] = "test-client-secret",
                ["OAuth:RedirectUri"] = "https://localhost/oauth/callback",
                ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
                ["Sql:ConnectionString"] = "Host=localhost;Port=5432;Database=poevaluationtest;Username=postgres;Password=postgres",
                ["Polling:Enabled"] = "false"
            });
        });

        return base.CreateHost(builder);
    }
}

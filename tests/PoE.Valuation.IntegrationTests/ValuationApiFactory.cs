using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Boots the real API host with in-memory configuration. Redis and SQL are never
/// touched at startup (lazy connections), so no external services are required.
/// The currency-exchange poller and the item catalogue refresher are disabled so tests
/// never hit live PoE endpoints.
/// </summary>
public sealed class ValuationApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Configuration every integration test host needs: valid options, unreachable-but-lazy backends, no background pollers.</summary>
    public static readonly Dictionary<string, string?> DefaultSettings = new()
    {
        ["OAuth:ClientId"] = "test-client-id",
        ["OAuth:ClientSecret"] = "test-client-secret",
        ["OAuth:RedirectUri"] = "https://localhost/oauth/callback",
        ["Redis:ConnectionString"] = "localhost:6379,abortConnect=false",
        ["Sql:ConnectionString"] = "Host=localhost;Port=5432;Database=poevaluationtest;Username=postgres;Password=postgres",
        ["Polling:Enabled"] = "false",
        ["Polling:ItemCatalogEnabled"] = "false"
    };

    public ValuationApiFactory() =>
        WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    /// <summary>Creates a factory for tests that manage the host lifetime explicitly (xunit disallows static members on IClassFixture types, so fixture-based tests use the constructor instead).</summary>
    public static ValuationApiFactory Create() => new();

    /// <summary>
    /// Creates a host whose registered services are replaced by <paramref name="configureServices"/>
    /// (in-memory fakes for the Redis/SQL interfaces), so a test can drive a route's happy path with
    /// no backend. <see cref="WebHostBuilderExtensions.ConfigureTestServices"/> applies after the
    /// application's own registrations, so the fake is the registration that resolves.
    /// </summary>
    public static WebApplicationFactory<Program> CreateWithServices(Action<IServiceCollection> configureServices) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(DefaultSettings));
            builder.ConfigureTestServices(configureServices);
        });

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(DefaultSettings));

        return base.CreateHost(builder);
    }
}

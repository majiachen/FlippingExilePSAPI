using PoE.Valuation.Api;
using PoE.Valuation.Api.Endpoints;
using PoE.Valuation.Api.Middleware;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.DependencyInjection;
using PoE.Valuation.Core.Logging;
using PoE.Valuation.Core.Validation;
using PoE.Valuation.Contracts.Health;
using PoE.Valuation.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Core services: strongly-typed options bound from configuration.
builder.Services.AddValuationCore(builder.Configuration);

// Replace the default logging providers with a single sanitized console sink so
// registered secrets can never reach an unsanitized provider.
builder.Logging.ClearProviders();
builder.Services.AddSanitizingLogging();

// Infrastructure: Redis + PostgreSQL (lazy connections).
builder.Services.AddValuationInfrastructure(builder.Configuration);

var app = builder.Build();

// Fail fast on misconfiguration before the app starts serving requests.
var validationErrors = ValuationOptionsValidator.Validate(
    builder.Configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions(),
    builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions(),
    builder.Configuration.GetSection(SqlOptions.SectionName).Get<SqlOptions>() ?? new SqlOptions(),
    builder.Configuration.GetSection(PoEApiOptions.SectionName).Get<PoEApiOptions>() ?? new PoEApiOptions());

if (validationErrors.Count > 0)
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    foreach (var error in validationErrors)
    {
        startupLogger.LogError("{Error}", error);
    }

    throw new InvalidOperationException($"Configuration is invalid:\n{string.Join(Environment.NewLine, validationErrors)}");
}

// Register secrets with the sanitizer so they can never appear in log output.
var sensitiveValues = app.Services.GetRequiredService<SensitiveValueStore>();
sensitiveValues.Add(builder.Configuration[OAuthOptions.SectionName + ":" + OAuthOptions.ClientSecretKey]);
sensitiveValues.Add(builder.Configuration[SqlOptions.SectionName + ":" + SqlOptions.ConnectionStringKey]);
sensitiveValues.Add(builder.Configuration[RedisOptions.SectionName + ":" + RedisOptions.ConnectionStringKey]);

// Translate Redis failures into HTTP 503 so an unavailable cache degrades gracefully instead
// of surfacing as a 500 (tech doc, sections 6.1 and 9).
app.UseMiddleware<RedisUnavailableMiddleware>();

// Translate Postgres failures on the /api surface into HTTP 503 (tech doc, section 6).
app.UseMiddleware<SqlUnavailableMiddleware>();

// Translate PoE REST API failures (league list / stash) into HTTP 502 (tech doc, section 6).
app.UseMiddleware<PoeUnavailableMiddleware>();

app.MapGet("/health", () => Results.Ok(new HealthStatusDto { TimestampUtc = DateTimeOffset.UtcNow }));

// Authenticated API + OAuth flow (tech doc, sections 5, 6 and 7).
app.MapAuthEndpoints();
app.MapApiEndpoints();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program { }

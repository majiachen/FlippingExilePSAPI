using FlippingExilesPublicStashAPI;
using FlippingExilesPublicStashAPI.API;
using FlippingExilesPublicStashAPI.Redis;

var builder = Host.CreateApplicationBuilder(args);
// Add logging configuration
builder.Logging.AddConsole(); // Add console logging (or any other logging provider)

// Register HttpClient with named options
builder.Services.AddHttpClient("PathOfExileClient",
    client => { client.DefaultRequestHeaders.Add("User-Agent", "YourAppName/1.0"); });

builder.Services.AddSingleton(sp =>
{
    var redisConnectionString = builder.Configuration["RedisConnectionString"];
    return new RedisMessage(redisConnectionString);
});

// Register OAuthTokenClient
builder.Services.AddSingleton(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("PathOfExileClient");

    var tokenUrl = "https://www.pathofexile.com/oauth/token";
    var clientId = builder.Configuration["OAuth:ClientId"];
    var clientSecret = builder.Configuration["OAuth:ClientSecret"];
    var logger = sp.GetRequiredService<ILogger<OAuthTokenClient>>();
    var redisMessage = sp.GetRequiredService<RedisMessage>();
    var itemFilter = sp.GetRequiredService<ItemFilter>();

    return new OAuthTokenClient(httpClient, tokenUrl, clientId, clientSecret, logger, redisMessage, itemFilter);
});

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TradeListHandler>>();
    var redisMessage = sp.GetRequiredService<RedisMessage>();
    return new TradeListHandler(logger, redisMessage);
});

// Register ItemFilter
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ItemFilter>>();
    var essenceTradeListHandler = sp.GetRequiredService<TradeListHandler>();
    return new ItemFilter(logger, essenceTradeListHandler);
});

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RateLimiter>>();
    return new RateLimiter(logger);
});

// Register the Worker service
builder.Services.AddHostedService<Worker>();

// Register background services
builder.Services.AddHostedService<LeagueBackgroundService>();
builder.Services.AddHostedService<AggregateBackgroundService>();

var host = builder.Build();
host.Run();
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using FlippingExilesPublicStashAPI;
using FlippingExilesPublicStashAPI.API;
using FlippingExilesPublicStashAPI.Redis;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
// Add logging configuration
builder.Logging.AddConsole(); // Add console logging (or any other logging provider)

// Register HttpClient with named options
builder.Services.AddHttpClient("PathOfExileClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "YourAppName/1.0");
});

builder.Services.AddSingleton<RedisMessage>(sp =>
{
    string redisConnectionString = builder.Configuration["RedisConnectionString"];
    var logger = sp.GetRequiredService<ILogger<RedisMessage>>();
    return new RedisMessage(redisConnectionString, logger);
});

// Register OAuthTokenClient
builder.Services.AddSingleton<OAuthTokenClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("PathOfExileClient");

    string tokenUrl = "https://www.pathofexile.com/oauth/token";
    string clientId = builder.Configuration["OAuth:ClientId"];
    string clientSecret = builder.Configuration["OAuth:ClientSecret"];
    var logger = sp.GetRequiredService<ILogger<OAuthTokenClient>>();
    var redisMessage = sp.GetRequiredService<RedisMessage>();
    var itemFilter = sp.GetRequiredService<ItemFilter>();
    
    return new OAuthTokenClient(httpClient, tokenUrl, clientId, clientSecret, logger, redisMessage, itemFilter);
});

builder.Services.AddSingleton<TradeListHandler>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TradeListHandler>>();
    var redisMessage = sp.GetRequiredService<RedisMessage>();
    return new TradeListHandler(logger, redisMessage);
});

// Register ItemFilter
builder.Services.AddSingleton<ItemFilter>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ItemFilter>>();
    var essenceTradeListHandler = sp.GetRequiredService<TradeListHandler>();
    return new ItemFilter(logger,essenceTradeListHandler);
});

builder.Services.AddSingleton<RateLimiter>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RateLimiter>>();
    return new RateLimiter(logger);
});

// Register the Worker service
builder.Services.AddHostedService<Worker>();

// builder.Services.AddHostedService<LeagueBackgroundService>(); commenting it out as /leagues scope is bugged and somehow no access

var host = builder.Build();
host.Run();
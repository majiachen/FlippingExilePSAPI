using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using FlippingExilesPublicStashAPI;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
var RedisConnectionString = builder.Configuration["RedisConnectionString"];
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
    string redisKey = "FlippingExilesPublicStashAPI";
    var logger = sp.GetRequiredService<ILogger<RedisMessage>>();
    return new RedisMessage(redisConnectionString,redisKey, logger);
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
    return new OAuthTokenClient(httpClient, tokenUrl, clientId, clientSecret, logger,redisMessage);
});



// Register the Worker service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
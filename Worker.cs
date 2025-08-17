
using FlippingExilesPublicStashAPI.API;
using FlippingExilesPublicStashAPI.Oauth;
using FlippingExilesPublicStashAPI.Redis;

namespace FlippingExilesPublicStashAPI;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly OAuthTokenClient _oauthClient;
    private readonly RedisMessage _redisMessage;
    private readonly RateLimiter _rateLimiter;
    
    
    public Worker(
        ILogger<Worker> logger,
        OAuthTokenClient oauthClient,
        RedisMessage redisMessage,
        RateLimiter rateLimiter)
    {
        _logger = logger;
        _oauthClient = oauthClient;
        _redisMessage = redisMessage;
        _rateLimiter = rateLimiter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _redisMessage.SetMessage(RedisMessageKeyHelper.GetTestDescription(),"test");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RateLimiter.WaitAsync(stoppingToken);
                // Call the API
                string apiResponse = await _oauthClient.GetPublicStashesAsync(stoppingToken);
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            }
        }
    }
}
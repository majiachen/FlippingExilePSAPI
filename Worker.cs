
using FlippingExilesPublicStashAPI.Oauth;

namespace FlippingExilesPublicStashAPI;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly OAuthTokenClient _oauthClient;
    private readonly RedisMessage _redisMessage;
    
    public Worker(
        ILogger<Worker> logger,
        OAuthTokenClient oauthClient,RedisMessage redisMessage)
    {
        _logger = logger;
        _oauthClient = oauthClient;
        _redisMessage = redisMessage;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _redisMessage.SetMessage("test");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Call the API
                string apiResponse = await _oauthClient.GetPublicStashesAsync(stoppingToken);
                _logger.LogInformation("API Response: {Response}", apiResponse);
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            }

            // Wait for a period before running again (e.g., 5 minutes)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
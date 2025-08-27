// This would be in a separate file like AggregateBackgroundService.cs
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using FlippingExilesPublicStashAPI.API;

public class AggregateBackgroundService : BackgroundService
{
    private readonly OAuthTokenClient _oauthClient;
    private readonly ILogger<AggregateBackgroundService> _logger;
    
    public AggregateBackgroundService(OAuthTokenClient oauthClient, ILogger<AggregateBackgroundService> logger)
    {
        _oauthClient = oauthClient;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _oauthClient.GetAggregateDataAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in aggregate background service");
                // Continue with next cycle even if there's an error
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}

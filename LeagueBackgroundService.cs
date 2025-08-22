// This would be in a separate file like LeagueBackgroundService.cs
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using FlippingExilesPublicStashAPI.API;

public class LeagueBackgroundService : BackgroundService
{
    private readonly OAuthTokenClient _oauthClient;
    private readonly ILogger<LeagueBackgroundService> _logger;
    
    public LeagueBackgroundService(OAuthTokenClient oauthClient, ILogger<LeagueBackgroundService> logger)
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
                await _oauthClient.GetLeaguesAsync(stoppingToken);
                // Wait for 1 hours before next execution
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in league background service");
                // Continue with next cycle even if there's an error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}

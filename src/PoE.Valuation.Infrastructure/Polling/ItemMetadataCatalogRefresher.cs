using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Infrastructure.Services;

namespace PoE.Valuation.Infrastructure.Polling;

/// <summary>
/// Background service that keeps <c>item_metadata</c> (the name → metadata path catalogue the lookup
/// endpoints depend on) from going stale. No-ops when <c>Polling:ItemCatalogEnabled=false</c>, which
/// is how the test host keeps the refresher from touching live PoE endpoints.
/// </summary>
public sealed class ItemMetadataCatalogRefresher : BackgroundService
{
    private readonly ItemMetadataCatalogService _catalog;
    private readonly PollingOptions _options;
    private readonly ILogger<ItemMetadataCatalogRefresher> _logger;

    public ItemMetadataCatalogRefresher(
        ItemMetadataCatalogService catalog,
        IOptions<PollingOptions> options,
        ILogger<ItemMetadataCatalogRefresher> logger)
    {
        _catalog = catalog;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ItemCatalogEnabled)
        {
            _logger.LogInformation("Item catalogue refresh is disabled (Polling:ItemCatalogEnabled=false); refresher will not run.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.ItemCatalogIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _catalog.RefreshIfStaleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Transient failures (network, SQL, Redis) are logged and retried on the next tick;
                // a stale catalogue degrades the lookup endpoints, it must not take the host down.
                _logger.LogError(ex, "Item catalogue refresh failed; retrying in {Interval}.", interval);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Services;

/// <summary>
/// Refreshes the item catalogue (name → metadata path) from the PoE trade data endpoint.
/// <para>
/// The refresh is a full swap of <c>item_metadata</c>, so it must run on exactly one instance at a
/// time: it is guarded by the Redis lock <c>lock:item-catalog</c>. A tick that finds the catalogue
/// younger than <see cref="PollingOptions.ItemCatalogMaxAgeSeconds"/> does no work at all, which is
/// what makes it cheap to call from both the background service and the manual admin endpoint.
/// </para>
/// </summary>
public sealed class ItemMetadataCatalogService
{
    /// <summary>How long the refresh lock survives if the holder dies mid-refresh.</summary>
    private static readonly TimeSpan RefreshLockTtl = TimeSpan.FromMinutes(10);

    private readonly PoeItemCatalogClient _client;
    private readonly IItemMetadataStore _store;
    private readonly IDistributedLock _lock;
    private readonly PollingOptions _options;
    private readonly ILogger<ItemMetadataCatalogService> _logger;

    public ItemMetadataCatalogService(
        PoeItemCatalogClient client,
        IItemMetadataStore store,
        IDistributedLock distributedLock,
        IOptions<PollingOptions> options,
        ILogger<ItemMetadataCatalogService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _lock = distributedLock ?? throw new ArgumentNullException(nameof(distributedLock));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    /// <summary>
    /// Refreshes the catalogue unless it is younger than the configured maximum age.
    /// </summary>
    /// <returns>Rows written, or 0 when the catalogue was fresh or another instance held the lock.</returns>
    public async Task<int> RefreshIfStaleAsync(CancellationToken ct = default) =>
        await RefreshAsync(force: false, ct);

    /// <summary>Refreshes the catalogue regardless of its age (the manual admin path).</summary>
    public async Task<int> RefreshAsync(CancellationToken ct = default) =>
        await RefreshAsync(force: true, ct);

    private async Task<int> RefreshAsync(bool force, CancellationToken ct)
    {
        if (!force)
        {
            var maxAge = TimeSpan.FromSeconds(Math.Max(1, _options.ItemCatalogMaxAgeSeconds));
            if (await _store.GetCatalogUpdatedAtAsync(ct) is { } updatedAt
                && DateTimeOffset.UtcNow - updatedAt < maxAge)
            {
                _logger.LogDebug("Item catalogue is fresh (refreshed {UpdatedAt}); no refresh needed.", updatedAt);
                return 0;
            }
        }

        if (!await _lock.TryAcquireAsync(RedisKeys.ItemCatalogLockKey, RefreshLockTtl, ct))
        {
            _logger.LogInformation("Item catalogue refresh skipped: another instance holds the refresh lock.");
            return 0;
        }

        try
        {
            var entries = await _client.GetCatalogAsync(ct);
            var written = await _store.ReplaceCatalogAsync(entries, DateTimeOffset.UtcNow, ct);

            _logger.LogInformation("Item catalogue refreshed: {Count} entries stored.", written);
            return written;
        }
        finally
        {
            await _lock.ReleaseAsync(RedisKeys.ItemCatalogLockKey, ct);
        }
    }
}

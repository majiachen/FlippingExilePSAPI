using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Validation;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Read client for the PoE trade data catalogue (<c>GET /api/trade/data/static</c>, no
/// authentication). The catalogue is the only public source that pairs an item's display name with
/// the metadata path the currency-exchange snapshots are keyed by, which is what lets a stash item
/// (<c>name</c>/<c>base_type</c>) be valued at all.
/// Base address is <c>PathOfExile:TradeDataBaseUrl</c>.
/// </summary>
public sealed class PoeItemCatalogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<PoeItemCatalogClient> _logger;

    public PoeItemCatalogClient(HttpClient http, ILogger<PoeItemCatalogClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches the catalogue and returns one <see cref="ItemMetadataEntry"/> per distinct metadata
    /// path. Entries with no icon, no name, or an icon that does not decode to an item asset path are
    /// skipped and counted in the log line rather than failing the refresh.
    /// </summary>
    /// <exception cref="HttpRequestException">Non-2xx response or a transport failure.</exception>
    public async Task<IReadOnlyList<ItemMetadataEntry>> GetCatalogAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("static", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TradeStaticResponse>(JsonOptions, ct);
        if (payload?.Result is not { Count: > 0 } categories)
            throw new InvalidOperationException("Item catalogue response contained no categories.");

        var entries = new List<ItemMetadataEntry>();
        var seenMetadata = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;

        foreach (var category in categories)
        {
            if (category.Entries is not { } items)
                continue;

            foreach (var item in items)
            {
                if (!TradeStaticMetadataDecoder.TryDecodeMetadata(item.Image, out var metadata))
                {
                    skipped++;
                    continue;
                }

                var displayName = item.Text?.Trim();
                if (string.IsNullOrEmpty(displayName))
                {
                    skipped++;
                    continue;
                }

                var lookupKey = ItemNameNormalizer.Normalize(displayName);
                if (lookupKey.Length == 0)
                {
                    skipped++;
                    continue;
                }

                // The same item can be listed under two categories; the first occurrence wins.
                if (!seenMetadata.Add(metadata))
                    continue;

                entries.Add(new ItemMetadataEntry(
                    metadata, displayName, category.Id ?? string.Empty, item.Id?.Trim(), lookupKey));
            }
        }

        if (entries.Count == 0)
            throw new InvalidOperationException(
                $"Item catalogue contained no decodable entries ({skipped} skipped).");

        _logger.LogInformation(
            "Item catalogue fetched: {EntryCount} entries from {CategoryCount} categories, {SkippedCount} skipped.",
            entries.Count, categories.Count, skipped);

        return entries;
    }
}

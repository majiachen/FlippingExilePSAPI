using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Client for the public, unauthenticated currency-exchange endpoint. The HttpClient base address
/// is configured in DI to <c>PoEApiOptions:CurrencyExchangeBaseUrl</c>; this client only appends the
/// hourly change id.
/// </summary>
public sealed class PoeCurrencyExchangeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public PoeCurrencyExchangeClient(HttpClient http) => _http = http;

    /// <summary>
    /// Fetches the digest for the hour starting at <paramref name="changeId"/> (Unix seconds).
    /// Returns <see cref="CurrencyExchangeResult.NotReady"/> when the API answers 404, i.e. the hour
    /// is still in progress or has no published data yet.
    /// </summary>
    public async Task<CurrencyExchangeResult> FetchDigestAsync(long changeId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"{changeId}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return CurrencyExchangeResult.NotReady;

        response.EnsureSuccessStatusCode();

        var digest = await response.Content.ReadFromJsonAsync<CurrencyExchangeResponse>(JsonOptions, cancellationToken)
                   ?? throw new InvalidOperationException("Currency exchange response deserialized to null.");

        return new CurrencyExchangeResult.Ok(digest.NextChangeId, digest);
    }
}

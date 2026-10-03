using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Read client for the PoE REST API endpoints the /api surface needs: the league list
/// (<c>GET /leagues</c>, public) and the account stash (<c>GET /stash/{leagueId}</c>, Bearer
/// access token). Base address is <c>PathOfExile:ApiBaseUrl</c> (see
/// <c>InfrastructureServiceCollectionExtensions</c>).
/// </summary>
public sealed class PoeStashClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public PoeStashClient(HttpClient http) =>
        _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>Fetches the league list (id + name pairs) the league-name → id mapping resolves against.</summary>
    public async Task<IReadOnlyList<LeagueInfo>> GetLeaguesAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("leagues", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<LeagueInfo>>(JsonOptions, ct)
               ?? new List<LeagueInfo>();
    }

    /// <summary>Fetches all items in the account's stash tabs for the league; the caller's access token is sent as Bearer.</summary>
    public async Task<IReadOnlyList<StashApiItem>> GetStashAsync(int leagueId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"stash/{leagueId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<StashApiItem>>(JsonOptions, ct)
               ?? new List<StashApiItem>();
    }
}

/// <summary>One entry of the PoE <c>GET /leagues</c> response.</summary>
/// <param name="Id">Numeric league id (required by <c>GET /stash/{leagueId}</c>).</param>
/// <param name="Name">League name as used in the DB and the public API (e.g. <c>Standard</c>).</param>
public sealed record LeagueInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

/// <summary>
/// One item of the PoE <c>GET /stash/{leagueId}</c> response (subset the stash endpoint exposes;
/// the PoE API returns many more fields that the public API intentionally does not).
/// </summary>
public sealed record StashApiItem(
    [property: JsonPropertyName("item_id")] string ItemId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("base_type")] string BaseType,
    [property: JsonPropertyName("ilvl")] int ItemLevel,
    [property: JsonPropertyName("frame_type")] int FrameType,
    [property: JsonPropertyName("stack_size")] int? StackSize,
    [property: JsonPropertyName("gem_level")] int? GemLevel,
    [property: JsonPropertyName("gem_quality")] int? GemQuality,
    [property: JsonPropertyName("corrupted")] bool Corrupted,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("note")] string? Note);

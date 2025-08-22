using Duende.IdentityModel.Client;
using FlippingExilesPublicStashAPI.LeaguePOCO;
using FlippingExilesPublicStashAPI.Oauth;
using FlippingExilesPublicStashAPI.PublicStashPOCO;
using FlippingExilesPublicStashAPI.Redis;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

namespace FlippingExilesPublicStashAPI.API;

public class OAuthTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<OAuthTokenClient> _logger;
    private string _accessToken;
    private readonly RedisMessage _redisMessage;
    private string changeId;
    private readonly ItemFilter _itemFilter;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromDays(1);


    public OAuthTokenClient(HttpClient httpClient, string tokenUrl, string clientId, string clientSecret, ILogger<OAuthTokenClient> logger, RedisMessage redisMessage, ItemFilter itemFilter)
    {
        _httpClient = httpClient;
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _logger = logger;
        _redisMessage = redisMessage;
        _itemFilter = itemFilter;
    }


    public async Task<string> GetPublicStashesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            changeId = _redisMessage.GetMessage(RedisMessageKeyHelper.GetChangeIdRedisKey());
            _logger.LogInformation("changed ID Acquired: "+  changeId);

            
            if (string.IsNullOrEmpty(_accessToken))
            {
                await SetAccessTokenAsync(cancellationToken);
            }

            
            const string apiUrl = "https://api.pathofexile.com/public-stash-tabs";
            
            var parameters = (new Dictionary<string, string>
                {
                    ["id"] = changeId
                }).Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value!);
            
            string requestUri = QueryHelpers.AddQueryString(apiUrl, parameters);
            
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("User-Agent", "OAuth flippingexiles/1.0 (contact: jackma790@gmail.com)");
            // Send the request and get the response as a stream
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                UpdateRateLimit(response);
                // Process the response as a stream
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var serializer = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };
                    var stashResponse = serializer.Deserialize<StashResponse>(jsonReader);
                    if (stashResponse != null)
                    {
                        _logger.LogInformation("changed ID Updated: "+  stashResponse.NextChangeId);
                        _redisMessage.SetMessage(RedisMessageKeyHelper.GetChangeIdRedisKey(),stashResponse.NextChangeId);
                    }

                    if (stashResponse?.Stashes == null) return "Stream processing completed.";
            
                    _itemFilter.FilterStashes(stashResponse.Stashes);

                }

                return "Stream processing completed.";
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                // Log the error
                _logger.LogError("API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", response.StatusCode, errorResponse);

                // Throw a custom exception
                throw new ApiException(response.StatusCode, errorResponse);
            }
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            throw; // Re-throw the exception
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            throw; // Re-throw the exception
        }
    }
    
    public async Task<string> GetLeaguesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedData = _redisMessage.GetMessage(RedisMessageKeyHelper.GetLeagueNameRedisKey());
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("Returning cached league data.");
                return "Cached league data returned.";
            }

            if (string.IsNullOrEmpty(_accessToken))
            {
                await SetAccessTokenAsync(cancellationToken);
            }

            
            const string apiUrl = "https://api.pathofexile.com/leagues";
            
            var parameters = (new Dictionary<string, string>
                {
                    ["type"] = "main"
                }).Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value!);
            
            string requestUri = QueryHelpers.AddQueryString(apiUrl, parameters);
            
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("User-Agent", "OAuth flippingexiles/1.0 (contact: jackma790@gmail.com)");
            // Send the request and get the response as a stream
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                UpdateRateLimit(response);
                // Process the response as a stream
                using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var serializer = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore
                    };
                    var leagueResponse = serializer.Deserialize<LeagueResponse>(jsonReader);
                    if (leagueResponse != null)
                    {
                        _logger.LogInformation("league Updated: "+  leagueResponse);
                        var serializedData = JsonConvert.SerializeObject(leagueResponse);
                        LeagueHelper.LeaguesList = leagueResponse.Leagues;
                        _redisMessage.SetMessage(RedisMessageKeyHelper.GetLeagueNameRedisKey(), serializedData, _cacheExpiry);
                    }

                }

                return "Stream league processing completed.";
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                // Log the error
                _logger.LogError("API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", response.StatusCode, errorResponse);

                // Throw a custom exception
                throw new ApiException(response.StatusCode, errorResponse);
            }
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            throw; // Re-throw the exception
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            throw; // Re-throw the exception
        }
    }


    private async Task<TokenResponse> SetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Use IdentityModel to request a token
        var tokenResponse = await _httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = _tokenUrl,
            ClientId = _clientId,
            ClientSecret = _clientSecret,
            Scope = "service:psapi" // Specify the required scope
        }, cancellationToken);

        if (tokenResponse.IsError)
        {
            _logger.LogError("Failed to acquire access token. Error: {Error}", tokenResponse.Error);
            throw new Exception($"Failed to acquire access token: {tokenResponse.Error}");
        }

        _logger.LogInformation("Access token acquired successfully.");
        _accessToken = tokenResponse.AccessToken;
        return tokenResponse;
    }
    
    private void UpdateRateLimit(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Rate-Limit-Ip", out var limitValues))
        {
            var limitParts = limitValues.First().Split(',')[^1].Split(':').Select(int.Parse).ToArray();
            int maxRequests = limitParts[0]; // Max requests
            int windowSeconds = limitParts[1]; // Time window

            RateLimiter.UpdateRateLimits(maxRequests, windowSeconds);
            _logger.LogInformation("Rate limits updated: {MaxRequests} requests per {WindowSeconds} seconds", maxRequests, windowSeconds);

        }
    }
    
    private List<Item> FilterItems(List<Item> stashItems)
    {
        
        stashItems.RemoveAll(item => string.IsNullOrEmpty(item.Note));
        _logger.LogInformation("number of stashes after filterItems: "+stashItems.Count);
        stashItems.ForEach(item => _logger.LogInformation(item.ToString()));
        return stashItems;
    }
    
    private List<Item> FilterEssenceItems(List<Item> stashItems)
    {
        throw new NotImplementedException();
    }
    

}
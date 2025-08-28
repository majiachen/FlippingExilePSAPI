using Duende.IdentityModel.Client;
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
    private readonly TimeSpan _leagueCacheExpiry = TimeSpan.FromDays(1);
    private readonly TimeSpan _aggregateCacheExpiry = TimeSpan.FromHours(1);

    
    // Create separate rate limiters for each API endpoint
    private readonly RateLimiter _publicStashRateLimiter;
    private readonly RateLimiter _leaguesRateLimiter;
    private readonly RateLimiter _aggregateRateLimiter;

    public OAuthTokenClient(HttpClient httpClient, string tokenUrl, string clientId, string clientSecret, ILogger<OAuthTokenClient> logger, RedisMessage redisMessage, ItemFilter itemFilter)
    {
        _httpClient = httpClient;
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _logger = logger;
        _redisMessage = redisMessage;
        _itemFilter = itemFilter;
        
        // Initialize separate rate limiters for each API endpoint
        _publicStashRateLimiter = new RateLimiter(logger); 
        _leaguesRateLimiter = new RateLimiter(logger);     
        _aggregateRateLimiter = new RateLimiter(logger);  
    }
    
    private async Task<TokenResponse> SetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Use IdentityModel to request a token
        var tokenResponse = await _httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = _tokenUrl,
            ClientId = _clientId,
            ClientSecret = _clientSecret,
            Scope = "service:psapi service:leagues service:cxapi" // Specify the required scope
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
    
    public async Task<string> GetPublicStashesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            changeId = _redisMessage.GetMessage(RedisMessageKeyHelper.GetChangeIdRedisKey());
            _logger.LogInformation("changed ID Acquired: " + changeId);

            var parameters = new Dictionary<string, string>
            {
                ["id"] = changeId
            };

            var response = await MakeApiRequestAsync("https://api.pathofexile.com/public-stash-tabs", parameters, cancellationToken, _publicStashRateLimiter);
            
            return await ProcessStashResponseAsync(response, cancellationToken);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    public async Task<string> GetLeaguesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedData = _redisMessage.GetMessage(RedisMessageKeyHelper.GetLeagueNameRedisKey());
            if (!string.IsNullOrEmpty(cachedData))
            {
                var leagueResponse = JsonConvert.DeserializeObject<List<League>>(cachedData);
                POCOHelper.LeaguesList = leagueResponse;
                _logger.LogInformation("Returning cached league data.");
                return "Cached league data returned.";
            }

            var parameters = new Dictionary<string, string>()
            {
                ["type"] = "main"
            };
            var response = await MakeApiRequestAsync("https://api.pathofexile.com/leagues", parameters, cancellationToken, _leaguesRateLimiter);
            
            return await ProcessLeagueResponseAsync(response, cancellationToken);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            throw;
        }
    }
    
    public async Task<string> GetAggregateDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cxApiChangeID = _redisMessage.GetMessage(RedisMessageKeyHelper.GetCXApiChangeIDRedisKey());
            var leagueMarketCache = _redisMessage.GetMessage(RedisMessageKeyHelper.GetCXApiRedisKey());
            if (!string.IsNullOrEmpty(leagueMarketCache) && IsEpochWithinCurrentHour(Convert.ToInt64(cxApiChangeID)))
            {
                var leagueMarketData = JsonConvert.DeserializeObject<LeagueMarketData>(leagueMarketCache);
                POCOHelper.MarketData = leagueMarketData;
                _logger.LogInformation("Returning cached league data.");
                return "Cached league data returned.";

            }else{

                // Remove the parameters dictionary since we're using path parameter
                // var parameters = new Dictionary<string, string>()
                // {
                //     ["id"] = "1722031200"//cxApiChangeID,
                // };
                // Use cxApiChangeID as part of the URL path
                var apiUrl = $"https://api.pathofexile.com/currency-exchange/{cxApiChangeID}";
                var response = await MakeApiRequestAsync(apiUrl, null, cancellationToken, _aggregateRateLimiter);

                return await ProcessCXApiResponseAsync(response, cancellationToken);
            }
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", ex.StatusCode, ex.ErrorResponse);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred: {ErrorMessage}", ex.Message);
            throw;
        }
    }
    
    private async Task<HttpResponseMessage> MakeApiRequestAsync(string apiUrl, Dictionary<string, string> parameters, CancellationToken cancellationToken, RateLimiter rateLimiter)
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            await SetAccessTokenAsync(cancellationToken);
        }
        await rateLimiter.WaitAsync(cancellationToken);

        string requestUri;
        if (parameters!=null && parameters.Count>0)
        {
            requestUri = QueryHelpers.AddQueryString(apiUrl, parameters);
        }
        else
        {
            requestUri = apiUrl;
        }
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("User-Agent", "OAuth flippingexiles/1.0 (contact: jackma790@gmail.com)");
        
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<string> ProcessStashResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            UpdateRateLimit(response, _publicStashRateLimiter);
            
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
                    _logger.LogInformation("changed ID Updated: " + stashResponse.NextChangeId);
                    _redisMessage.SetMessage(RedisMessageKeyHelper.GetChangeIdRedisKey(), stashResponse.NextChangeId);
                }

                if (stashResponse?.Stashes == null) return "Stream processing completed.";

                _itemFilter.FilterStashes(stashResponse.Stashes);
            }

            return "Stream processing completed.";
        }
        else
        {
            string errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError("API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", response.StatusCode, errorResponse);

            throw new ApiException(response.StatusCode, errorResponse);
        }
    }

    private async Task<string> ProcessLeagueResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            UpdateRateLimit(response, _leaguesRateLimiter);
            
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var serializer = new JsonSerializer
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var leagueResponse = serializer.Deserialize<List<League>>(jsonReader);
                
                _logger.LogInformation("league Updated: " + leagueResponse);
                var serializedData = JsonConvert.SerializeObject(leagueResponse);
                POCOHelper.LeaguesList = leagueResponse;
                _redisMessage.SetMessage(RedisMessageKeyHelper.GetLeagueNameRedisKey(), serializedData, _leagueCacheExpiry);
                
            }

            return "Stream league processing completed.";
        }
        else
        {
            string errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError("API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", response.StatusCode, errorResponse);

            throw new ApiException(response.StatusCode, errorResponse);
        }
    }
    
    private async Task<string> ProcessCXApiResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var serializer = new JsonSerializer
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                var leagueMarketData = serializer.Deserialize<LeagueMarketData>(jsonReader);
                _redisMessage.SetMessage(RedisMessageKeyHelper.GetCXApiChangeIDRedisKey(),leagueMarketData.next_change_id.ToString());
                

                _logger.LogInformation("CX Api Updated: " + leagueMarketData);
                var serializedData = JsonConvert.SerializeObject(leagueMarketData);
                POCOHelper.MarketData = leagueMarketData;
                _redisMessage.SetMessage(RedisMessageKeyHelper.GetCXApiRedisKey(), serializedData, _leagueCacheExpiry);
                
            }

            return "Stream league processing completed.";
        }
        else
        {
            string errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError("API request failed. Status Code: {StatusCode}. Error Response: {ErrorResponse}", response.StatusCode, errorResponse);

            throw new ApiException(response.StatusCode, errorResponse);
        }
    }
    
    private void UpdateRateLimit(HttpResponseMessage response, RateLimiter rateLimiter)
    {
        if (response.Headers.TryGetValues("X-Rate-Limit-Ip", out var limitValues))
        {
            var limitParts = limitValues.First().Split(',')[^1].Split(':').Select(int.Parse).ToArray();
            int maxRequests = limitParts[0]; // Max requests
            int windowSeconds = limitParts[1]; // Time window

            rateLimiter.UpdateRateLimits(maxRequests, windowSeconds);
            _logger.LogInformation("Rate limits updated for endpoint: {MaxRequests} requests per {WindowSeconds} seconds", maxRequests, windowSeconds);
        }
    }
    
    public bool IsEpochWithinCurrentHour(long epochTimestamp)
    {
        long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long oneHourInSeconds = 3600;
    
        return epochTimestamp >= (currentEpoch - oneHourInSeconds) && epochTimestamp <= currentEpoch;
    }
}
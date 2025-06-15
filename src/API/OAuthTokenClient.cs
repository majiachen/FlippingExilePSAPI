

using Duende.IdentityModel.Client;
using FlippingExilesPublicStashAPI.Oauth;
using FlippingExilesPublicStashAPI.PublicStashPOCO;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;

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

    public OAuthTokenClient(HttpClient httpClient, string tokenUrl, string clientId, string clientSecret, ILogger<OAuthTokenClient> logger,RedisMessage redisMessage)
    {
        _httpClient = httpClient;
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _logger = logger;
        _redisMessage = redisMessage;
    }

    public async Task<string> GetPublicStashesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            changeId = _redisMessage.GetMessage();
            _logger.LogInformation("changed ID Acquired: "+  changeId);

            
            if (string.IsNullOrEmpty(_accessToken))
            {
                var tokenResponse = await RequestAccessTokenAsync(cancellationToken);
                _accessToken = tokenResponse.AccessToken; // Store the access token
                _logger.LogInformation("Access Token Acquired!");
            }

            
            const string apiUrl = "https://api.pathofexile.com/public-stash-tabs";
            
            var parameters = (new Dictionary<string, string>
            {
                ["next_change_id"] = changeId
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
                // Process the response as a stream
                using (var stream = await response.Content.ReadAsStreamAsync())
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
                        _redisMessage.SetMessage(stashResponse.NextChangeId);
                    }
                    if (stashResponse?.Stashes != null)
                    {
                        foreach (var stash in stashResponse.Stashes)
                        {
                            if (stash.Items != null)
                            {
                                // Example filtering - you can customize this
                                stash.Items = FilterItems(stash.Items);
                        
                                // Additional processing
                                ProcessItems(stash.Items);
                            }
                        }
                    }
                }

                return "Stream processing completed.";
            }
            else
            {
                string errorResponse = await response.Content.ReadAsStringAsync();

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


    private async Task<TokenResponse> RequestAccessTokenAsync(CancellationToken cancellationToken = default)
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
        return tokenResponse;
    }
    
    private List<Item> FilterItems(List<Item> stashItems)
    {
        throw new NotImplementedException();
    }
    
    
    private void ProcessItems(List<Item> stashItems)
    {
        throw new NotImplementedException();
    }

}
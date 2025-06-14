

using Duende.IdentityModel.Client;
using FlippingExilesPublicStashAPI.Oauth;

public class OAuthTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly ILogger<OAuthTokenClient> _logger;
    private string _accessToken;
    

    public OAuthTokenClient(HttpClient httpClient, string tokenUrl, string clientId, string clientSecret, ILogger<OAuthTokenClient> logger)
    {
        _httpClient = httpClient;
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _logger = logger;
    }

    public async Task<string> GetPublicStashesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get the access token (if not already acquired)
            if (string.IsNullOrEmpty(_accessToken))
            {
                var tokenResponse = await RequestAccessTokenAsync(cancellationToken);
                _accessToken = tokenResponse.AccessToken; // Store the access token
                _logger.LogInformation("Access Token Acquired!");
            }

            // Step 2: Use the access token to call the streaming API
            string apiUrl = "https://api.pathofexile.com/public-stash-tabs";

            // Create a new HttpRequestMessage to include the Authorization header
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Add("User-Agent", "OAuth flippingexiles/1.0 (contact: jackma790@gmail.com)");

            // Send the request and get the response as a stream
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Process the response as a stream
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // Process each line of the stream
                        _logger.LogInformation("Received data: {Line}", line);
                        
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
}
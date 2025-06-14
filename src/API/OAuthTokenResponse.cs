using Newtonsoft.Json;

namespace FlippingExilesPublicStashAPI.Oauth;

public class OAuthTokenResponse
{
    [JsonProperty("access_token")]
    public required string AccessToken { get; set; }

    [JsonProperty("expires_in")]
    public int? ExpiresIn { get; set; } // Nullable int for "expires_in": null

    [JsonProperty("token_type")]
    public required string TokenType { get; set; }

    [JsonProperty("username")]
    public required string Username { get; set; }

    [JsonProperty("sub")]
    public required string Sub { get; set; }

    [JsonProperty("scope")]
    public required string Scope { get; set; }
}
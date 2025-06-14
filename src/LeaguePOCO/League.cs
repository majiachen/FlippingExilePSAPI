using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.LeaguePOCO;

public class League
{
    [JsonPropertyName("id")]
    public string Id { get; set; } // Unique ID of the league

    [JsonPropertyName("name")]
    public string Name { get; set; } // Name of the league

    [JsonPropertyName("description")]
    public string Description { get; set; } // Description of the league

    [JsonPropertyName("startAt")]
    public DateTimeOffset StartAt { get; set; } // Start date of the league

    [JsonPropertyName("endAt")]
    public DateTimeOffset EndAt { get; set; } // End date of the league

    [JsonPropertyName("public")]
    public bool IsPublic { get; set; } // Whether the league is public

    [JsonPropertyName("rules")]
    public string Rules { get; set; } // Rules of the league

    [JsonPropertyName("tradeUrl")]
    public string TradeUrl { get; set; } // URL for trading in the league

    [JsonPropertyName("registerUrl")]
    public string RegisterUrl { get; set; } // URL for registering in the league
}
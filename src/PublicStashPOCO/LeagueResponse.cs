using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class LeagueResponse
{
    [JsonPropertyName("leagues")]
    public List<League> Leagues { get; set; } // List of leagues
}
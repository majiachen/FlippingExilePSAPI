using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.LeaguePOCO;

public class LeagueResponse
{
    [JsonPropertyName("leagues")]
    public List<League> Leagues { get; set; } // List of leagues
}
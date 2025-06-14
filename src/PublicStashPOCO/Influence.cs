using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Influence
{
    [JsonPropertyName("influenceType")]
    public string InfluenceType { get; set; } // Type of influence (e.g., "Shaper", "Elder")
}
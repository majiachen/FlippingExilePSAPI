using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Requirement
{
    [JsonPropertyName("name")]
    public string Name { get; set; } // Name of the requirement

    [JsonPropertyName("values")]
    public List<object> Values { get; set; } // Values of the requirement

    [JsonPropertyName("displayMode")]
    public int DisplayMode { get; set; } // Display mode of the requirement
}
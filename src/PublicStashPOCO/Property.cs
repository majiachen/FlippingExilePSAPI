using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Property
{
    [JsonPropertyName("name")]
    public string Name { get; set; } // Name of the property

    [JsonPropertyName("values")]
    public List<object> Values { get; set; } // Values of the property

    [JsonPropertyName("displayMode")]
    public int DisplayMode { get; set; } // Display mode of the property
}
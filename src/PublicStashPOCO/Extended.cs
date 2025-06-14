using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Extended
{
    [JsonPropertyName("category")]
    public string Category { get; set; } // Category of the extended property

    [JsonPropertyName("subcategories")]
    public List<string> Subcategories { get; set; } // Subcategories of the extended property
}
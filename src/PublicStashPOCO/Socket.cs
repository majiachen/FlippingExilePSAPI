using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Socket
{
    [JsonPropertyName("group")]
    public int Group { get; set; } // Socket group

    [JsonPropertyName("attr")]
    public string Attribute { get; set; } // Socket attribute (e.g., "R", "G", "B")
}
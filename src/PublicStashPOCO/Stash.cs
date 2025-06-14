using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Stash
{
    [JsonPropertyName("id")]
    public string Id { get; set; } // Unique ID of the stash

    [JsonPropertyName("public")]
    public bool IsPublic { get; set; } // Whether the stash is public

    [JsonPropertyName("accountName")]
    public string AccountName { get; set; } // Account name of the stash owner

    [JsonPropertyName("lastCharacterName")]
    public string LastCharacterName { get; set; } // Last character name used by the account

    [JsonPropertyName("stash")]
    public string StashName { get; set; } // Name of the stash tab

    [JsonPropertyName("stashType")]
    public string StashType { get; set; } // Type of the stash tab (e.g., "PremiumStash")

    [JsonPropertyName("items")]
    public List<Item> Items { get; set; } // List of items in the stash
}
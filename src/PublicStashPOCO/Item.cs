using System.Text.Json.Serialization;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class Item
{
    [JsonPropertyName("verified")]
    public bool IsVerified { get; set; } // Whether the item is verified

    [JsonPropertyName("w")]
    public int Width { get; set; } // Width of the item

    [JsonPropertyName("h")]
    public int Height { get; set; } // Height of the item

    [JsonPropertyName("ilvl")]
    public int ItemLevel { get; set; } // Item level

    [JsonPropertyName("icon")]
    public string IconUrl { get; set; } // URL to the item's icon

    [JsonPropertyName("league")]
    public string League { get; set; } // League the item belongs to

    [JsonPropertyName("id")]
    public string Id { get; set; } // Unique ID of the item

    [JsonPropertyName("sockets")]
    public List<Socket> Sockets { get; set; } // List of sockets on the item

    [JsonPropertyName("name")]
    public string Name { get; set; } // Name of the item

    [JsonPropertyName("typeLine")]
    public string TypeLine { get; set; } // Type line of the item

    [JsonPropertyName("identified")]
    public bool IsIdentified { get; set; } // Whether the item is identified

    [JsonPropertyName("corrupted")]
    public bool IsCorrupted { get; set; } // Whether the item is corrupted

    [JsonPropertyName("stackSize")]
    public int StackSize { get; set; } // Stack size (for stackable items)

    [JsonPropertyName("maxStackSize")]
    public int MaxStackSize { get; set; } // Maximum stack size

    [JsonPropertyName("note")]
    public string Note { get; set; } // Custom note on the item

    [JsonPropertyName("properties")]
    public List<Property> Properties { get; set; } // List of item properties

    [JsonPropertyName("requirements")]
    public List<Requirement> Requirements { get; set; } // List of item requirements

    [JsonPropertyName("explicitMods")]
    public List<string> ExplicitMods { get; set; } // List of explicit modifiers

    [JsonPropertyName("implicitMods")]
    public List<string> ImplicitMods { get; set; } // List of implicit modifiers

    [JsonPropertyName("craftedMods")]
    public List<string> CraftedMods { get; set; } // List of crafted modifiers

    [JsonPropertyName("enchantMods")]
    public List<string> EnchantMods { get; set; } // List of enchant modifiers

    [JsonPropertyName("fracturedMods")]
    public List<string> FracturedMods { get; set; } // List of fractured modifiers

    [JsonPropertyName("frameType")]
    public int FrameType { get; set; } // Frame type of the item (e.g., 0 = Normal, 1 = Magic, 2 = Rare, 3 = Unique)

    [JsonPropertyName("artFilename")]
    public string ArtFilename { get; set; } // Art filename (for unique items)

    [JsonPropertyName("duplicated")]
    public bool IsDuplicated { get; set; } // Whether the item is duplicated

    [JsonPropertyName("influences")]
    public List<Influence> Influences { get; set; } // List of influences on the item

    [JsonPropertyName("extended")]
    public List<Extended> ExtendedProperties { get; set; } // Extended properties
}
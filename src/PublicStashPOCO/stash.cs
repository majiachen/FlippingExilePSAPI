using System.Text;
using Newtonsoft.Json;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class StashResponse
{
    [JsonProperty("next_change_id")]
    public string NextChangeId { get; set; }
    
    [JsonProperty("stashes")]
    public List<Stash> Stashes { get; set; }
}

public class Stash
{
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("public")]
    public bool Public { get; set; }
    
    [JsonProperty("accountName")]
    public string AccountName { get; set; }
    
    [JsonProperty("stash")]
    public string StashName { get; set; }
    
    [JsonProperty("stashType")]
    public string StashType { get; set; }
    
    [JsonProperty("league")]
    public string League { get; set; }
    
    [JsonProperty("items")]
    public List<Item> Items { get; set; }
    
    [JsonProperty("lastCharacterName")]
    public string LastCharacterName { get; set; }
}

public class Item
{
    // Core fields
    [JsonProperty("id")]
    public string Id { get; set; }
    
    [JsonProperty("verified")]
    public bool Verified { get; set; }
    
    [JsonProperty("w")]
    public int Width { get; set; }
    
    [JsonProperty("h")]
    public int Height { get; set; }
    
    [JsonProperty("icon")]
    public string Icon { get; set; }
    
    [JsonProperty("league")]
    public string League { get; set; }
    
    // Identification
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("typeLine")]
    public string TypeLine { get; set; }
    
    [JsonProperty("baseType")]
    public string BaseType { get; set; }
    
    [JsonProperty("identified")]
    public bool Identified { get; set; }
    
    [JsonProperty("itemLevel")]
    public int ItemLevel { get; set; }
    
    [JsonProperty("note")]
    public string Note { get; set; }
    
    [JsonProperty("flavourText")]
    public List<string> FlavourText { get; set; }
    
    [JsonProperty("descrText")]
    public string DescriptionText { get; set; }
    
    [JsonProperty("secDescrText")]
    public string SecondaryDescriptionText { get; set; }
    
    // Classification
    [JsonProperty("frameType")]
    public int FrameType { get; set; } // 0=normal, 1=magic, 2=rare, 3=unique, 4=gem, 5=currency
    
    [JsonProperty("rarity")]
    public string Rarity { get; set; }
    
    [JsonProperty("support")]
    public bool? IsSupportGem { get; set; }
    
    [JsonProperty("corrupted")]
    public bool? Corrupted { get; set; }
    
    [JsonProperty("talismanTier")]
    public int? TalismanTier { get; set; }
    
    [JsonProperty("prophecyText")]
    public string ProphecyText { get; set; }
    
    [JsonProperty("prophecyDiffText")]
    public string ProphecyDifficultyText { get; set; }
    
    // Positioning
    [JsonProperty("x")]
    public int XPosition { get; set; }
    
    [JsonProperty("y")]
    public int YPosition { get; set; }
    
    [JsonProperty("inventoryId")]
    public string InventoryId { get; set; }
    
    [JsonProperty("socket")]
    public int? Socket { get; set; }
    
    [JsonProperty("colour")]
    public string SocketColor { get; set; }
    
    // Item properties
    [JsonProperty("sockets")]
    public List<Socket> Sockets { get; set; }
    
    [JsonProperty("properties")]
    public List<Property> Properties { get; set; }
    
    [JsonProperty("additionalProperties")]
    public List<AdditionalProperty> AdditionalProperties { get; set; }
    
    [JsonProperty("requirements")]
    public List<Requirement> Requirements { get; set; }
    
    [JsonProperty("nextLevelRequirements")]
    public List<NextLevelRequirement> NextLevelRequirements { get; set; }
    
    // Modifiers
    [JsonProperty("implicitMods")]
    public List<string> ImplicitMods { get; set; }
    
    [JsonProperty("explicitMods")]
    public List<string> ExplicitMods { get; set; }
    
    [JsonProperty("craftedMods")]
    public List<string> CraftedMods { get; set; }
    
    [JsonProperty("enchantMods")]
    public List<string> EnchantMods { get; set; }
    
    [JsonProperty("utilityMods")]
    public List<string> UtilityMods { get; set; }
    
    [JsonProperty("fracturedMods")]
    public List<string> FracturedMods { get; set; }
    
    [JsonProperty("cosmeticMods")]
    public List<string> CosmeticMods { get; set; }
    
    // Socketed items
    [JsonProperty("socketedItems")]
    public List<SocketedItem> SocketedItems { get; set; }
    
    // Extended info
    [JsonProperty("extended")]
    public ExtendedInfo Extended { get; set; }
    
    // Incubator info
    [JsonProperty("incubatedItem")]
    public IncubatedItem IncubatedItem { get; set; }
    
    // For gems
    [JsonProperty("gemLevel")]
    public int? GemLevel { get; set; }
    
    [JsonProperty("gemQuality")]
    public int? GemQuality { get; set; }
    
    [JsonProperty("stackSize")]
    public int? StackSize { get; set; }
    
    [JsonProperty("maxStackSize")]
    public int? MaxStackSize { get; set; }
    
    // For flasks
    [JsonProperty("duplicated")]
    public bool? Duplicated { get; set; }
    
    [JsonProperty("artFilename")]
    public string ArtFilename { get; set; }
    public override string ToString()
    {
        var sb = new StringBuilder();
    
        // Basic identification
        if (!string.IsNullOrEmpty(Name))
        {
            sb.Append(Name);
            if (!string.IsNullOrEmpty(BaseType) && Name != BaseType)
                sb.Append($" ({BaseType})");
        }
        else if (!string.IsNullOrEmpty(TypeLine))
        {
            sb.Append(TypeLine);
        }
        else if (!string.IsNullOrEmpty(BaseType))
        {
            sb.Append(BaseType);
        }

        // Rarity and frame type
        if (!string.IsNullOrEmpty(Rarity))
        {
            sb.Append($" [{Rarity}]");
        }

        // Item level for non-currency items
        if (FrameType != 5 && ItemLevel > 0)
        {
            sb.Append($" (ilvl {ItemLevel})");
        }

        // Gem info
        if (GemLevel.HasValue || GemQuality.HasValue)
        {
            sb.Append(" [");
            if (GemLevel.HasValue) sb.Append($"Lvl {GemLevel.Value}");
            if (GemQuality.HasValue) sb.Append($" {GemQuality.Value}%");
            sb.Append("]");
        }

        // Stack info
        if (StackSize.HasValue && MaxStackSize.HasValue)
        {
            sb.Append($" ({StackSize.Value}/{MaxStackSize.Value})");
        }
        else if (StackSize.HasValue)
        {
            sb.Append($" ({StackSize.Value})");
        }

        // Special flags
        var specialFlags = new List<string>();
        if (Corrupted == true) specialFlags.Add("Corrupted");
        if (Verified) specialFlags.Add("Verified");
        if (Identified) specialFlags.Add("Identified");
        if (IsSupportGem == true) specialFlags.Add("Support");
        if (Duplicated == true) specialFlags.Add("Mirrored");
    
        if (specialFlags.Count > 0)
        {
            sb.Append(" [");
            sb.Append(string.Join(", ", specialFlags));
            sb.Append("]");
        }

        // Note
        if (!string.IsNullOrEmpty(Note))
        {
            sb.Append($" » {Note}");
        }

        // Position info
        if (!string.IsNullOrEmpty(InventoryId))
        {
            sb.Append($" @ {InventoryId}({XPosition},{YPosition})");
        }

        return sb.ToString();
    }
}

public class Socket
{
    [JsonProperty("group")]
    public int Group { get; set; }
    
    [JsonProperty("attr")]
    public string Attribute { get; set; }
    
    [JsonProperty("sColour")]
    public string Color { get; set; }
}

public class Property
{
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("values")]
    public List<List<object>> Values { get; set; }
    
    [JsonProperty("displayMode")]
    public int DisplayMode { get; set; }
    
    [JsonProperty("type")]
    public int? Type { get; set; }
    
    [JsonProperty("progress")]
    public double? Progress { get; set; }
}

public class AdditionalProperty : Property
{
    // Inherits all properties from Property
}

public class Requirement
{
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("values")]
    public List<List<object>> Values { get; set; }
    
    [JsonProperty("displayMode")]
    public int DisplayMode { get; set; }
    
    [JsonProperty("type")]
    public int? Type { get; set; }
    
    [JsonProperty("suffix")]
    public string Suffix { get; set; }
}

public class NextLevelRequirement : Requirement
{
    // Inherits all properties from Requirement
}

public class SocketedItem : Item
{
    // Inherits all properties from Item
}

public class ExtendedInfo
{
    [JsonProperty("category")]
    public string Category { get; set; }
    
    [JsonProperty("subcategories")]
    public List<string> Subcategories { get; set; }
    
    [JsonProperty("prefixes")]
    public int? Prefixes { get; set; }
    
    [JsonProperty("suffixes")]
    public int? Suffixes { get; set; }
    
    [JsonProperty("hashes")]
    public Dictionary<string, string> Hashes { get; set; }
}

public class IncubatedItem
{
    [JsonProperty("name")]
    public string Name { get; set; }
    
    [JsonProperty("level")]
    public int Level { get; set; }
    
    [JsonProperty("progress")]
    public double Progress { get; set; }
    
    [JsonProperty("total")]
    public double Total { get; set; }
}
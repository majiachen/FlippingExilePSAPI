using Newtonsoft.Json;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public class AccountAndStashMap
{
    [JsonProperty("accountName")]
    public string AccountName { get; set; }
    
    [JsonProperty("stashId")]
    public string StashId { get; set; }
    
    [JsonProperty("item")]
    public List<Item> Item { get; set; }
    
    public override string ToString()
    {
        return $"accountName: {AccountName}, Stash ID: {StashId}, Item: {Item}";
    }
}
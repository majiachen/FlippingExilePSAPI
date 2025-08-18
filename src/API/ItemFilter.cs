using FlippingExilesPublicStashAPI.PublicStashPOCO;
using Microsoft.Extensions.Logging;

public class ItemFilter
{
    private readonly ILogger<ItemFilter> _logger;

    public ItemFilter(ILogger<ItemFilter> logger)
    {
        _logger = logger;
    }



    public List<Stash> FilterStashes(List<Stash> stashes)
    {
        if (stashes == null) return new List<Stash>();
        
        return stashes.Where(stash => stash.Items is { Count: > 0 } && (
                stash.StashType.Contains("Currency") ||
                stash.StashType.Contains("Delve") ||
                stash.StashType.Contains("Essence") ||
                stash.StashType.Contains("Blight") ||
                stash.StashType.Contains("Delirium") ||
                stash.StashType.Contains("Fragment") ||
                stash.StashType.Contains("Ultimatum")))
            .ToList();
    }
    
    public void ProcessFilteredStashes(List<Stash> stashes, ILogger logger)
    {
        if (stashes == null) return;
        
        foreach (var stash in stashes)
        {
            var filteredItems = FilterItems(stash.Items);
            
            foreach (var stashItem in filteredItems)
            {
                logger.LogInformation("item found with note: " + stashItem +"at stash: "+stash.Id + "with stashtype: "+stash.StashType);
            }
        }
    }
    
    public List<Item> FilterItems(List<Item> stashItems)
    {
        if (stashItems == null) return new List<Item>();
        
        stashItems.RemoveAll(item => string.IsNullOrEmpty(item.Note));
        _logger.LogInformation("Number of items after filtering: {ItemCount}", stashItems.Count);
        stashItems.ForEach(item => _logger.LogInformation(item.ToString()));
        return stashItems;
    }
}
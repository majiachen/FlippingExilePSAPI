using FlippingExilesPublicStashAPI.PublicStashPOCO;
using Microsoft.Extensions.Logging;

public class ItemFilter
{
    private readonly ILogger<ItemFilter> _logger;

    public ItemFilter(ILogger<ItemFilter> logger)
    {
        _logger = logger;
    }



    public void FilterStashes(List<Stash> stashes)
    {
        if (stashes == null) return;
        
        ProcessFilteredStashes(stashes.Where(stash => stash.Items is { Count: > 0 } && (
                stash.StashType.Contains("Currency") ||
                stash.StashType.Contains("Delve") ||
                stash.StashType.Contains("Essence") ||
                stash.StashType.Contains("Blight") ||
                stash.StashType.Contains("Delirium") ||
                stash.StashType.Contains("Fragment") ||
                stash.StashType.Contains("Ultimatum")))
            .ToList());
    }
    
    public void ProcessFilteredStashes(List<Stash> stashes)
    {
        if (stashes == null) return;
    
        foreach (var stash in stashes)
        {
            // First filter for items with notes (creates a new list, doesn't modify original)
            var itemsWithNotes = FilterForItemsWithNotes(stash.Items);
        
            // Apply separate filters to the filtered list
            var essenceItems = FilterForEssences(itemsWithNotes);
            var fossilItems = FilterForFossils(itemsWithNotes);
            // Add more filters as needed
        
            // Process each filtered list separately
            ProcessEssenceItems(essenceItems, stash);
            ProcessFossilItems(fossilItems, stash);
        
            // Log all items with notes if needed
            foreach (var stashItem in itemsWithNotes)
            {
                _logger.LogInformation($"Item found with note: {stashItem} at stash: {stash.Id} with stash type: {stash.StashType}");
            }
        }
    }

    private List<Item> FilterForItemsWithNotes(List<Item> items)
    {
        return items?.Where(item => !string.IsNullOrEmpty(item.Note))?.ToList() ?? new List<Item>();
    }

    private List<Item> FilterForEssences(List<Item> items)
    {
        var essenceDescriptions = EnumExtensions.GetAllDescriptions<EssenceEnum>();
        return items.Where(item => essenceDescriptions.Contains(item.Name)).ToList();
    }

    private List<Item> FilterForFossils(List<Item> items)
    {
        var fossilDescriptions = EnumExtensions.GetAllDescriptions<FossilEnum>();
        return items.Where(item => fossilDescriptions.Contains(item.Name)).ToList();
    }

    private void ProcessEssenceItems(List<Item> essenceItems, Stash stash)
    {
        foreach (var item in essenceItems)
        {
            
        }
    }

    private void ProcessFossilItems(List<Item> fossilItems, Stash stash)
    {
        foreach (var item in fossilItems)
        {
            
        }
    }
    



}
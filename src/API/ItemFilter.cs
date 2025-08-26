using FlippingExilesPublicStashAPI.LeaguePOCO;
using FlippingExilesPublicStashAPI.PublicStashPOCO;
using FlippingExilesPublicStashAPI.Redis;
using Microsoft.Extensions.Logging;

public class ItemFilter
{
    private readonly ILogger<ItemFilter> _logger;
    private readonly TradeListHandler _tradeListHandler;

    public ItemFilter(ILogger<ItemFilter> logger, TradeListHandler tradeListHandler)
    {
        _logger = logger;
        _tradeListHandler = tradeListHandler;
    }



    public void FilterStashes(List<Stash> stashes)
    {
        
        if (stashes == null) return;
    
        ProcessFilteredStashes(stashes.Where(stash => 
                stash.Items is { Count: > 0 } && 
                stash.StashType != null && 
                LeagueHelper.LeaguesList.Any(league => league.Id.Equals(stash.League)) &&
                (stash.StashType.Contains("Currency", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Delve", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Essence", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Blight", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Delirium", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Fragment", StringComparison.CurrentCultureIgnoreCase) ||
                 stash.StashType.Contains("Ultimatum", StringComparison.CurrentCultureIgnoreCase) || 
                 (stash.StashType.Contains("PremiumStash", StringComparison.CurrentCultureIgnoreCase) &&
                  stash.Items.Any(item => 
                      item.BaseType != null && 
                      (item.BaseType.Contains("Fossil", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Essence Of", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Orb", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Oil", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Delirium Orb", StringComparison.CurrentCultureIgnoreCase) ||
                       item.Properties?.Any(p => p.Type is 32) == true))))) 
            .ToList());
    }
    
    public void ProcessFilteredStashes(List<Stash> stashes)
    {
        if (stashes == null) return;
    
        foreach (var stash in stashes)
        {
            // First filter for items with notes (creates a new list, doesn't modify original)
            var itemsWithNotes = FilterForItemsWithNotes(stash.Items);
            if (itemsWithNotes.Count >0)
            {
                _logger.LogInformation("item with notes: " + string.Join(",", itemsWithNotes.Select(
                    i=> $"name: {i.Name} + basetype : {i.BaseType} + price : {i.Note}")));
                // Apply separate filters to the filtered list
                var essenceItems = FilterForEssences(itemsWithNotes);
                var fossilItems = FilterForFossils(itemsWithNotes);
                var scarabItems = FilterForScarabs(itemsWithNotes);
                var oilItems = FilterForOils(itemsWithNotes);
            
                // Add more filters as needed
        
                // Process each filtered list separately
                
                if (essenceItems.Any()) 
                {
                    ProcessEssenceItems(essenceItems, stash);
                }
                if (fossilItems.Any()) 
                {
                    ProcessFossilItems(fossilItems, stash);
                }
                if (scarabItems.Any()) 
                {
                    ProcessScarabItems(scarabItems, stash);
                }
                if (oilItems.Any()) 
                {
                    ProcessOilItems(oilItems, stash);
                }

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
        var essenceSet = new HashSet<string>(essenceDescriptions, StringComparer.OrdinalIgnoreCase);
    
        return items.Where(item => 
            essenceSet.Any(description => item.BaseType.Contains(description))
        ).ToList();
    }

    private List<Item> FilterForFossils(List<Item> items)
    {
        var fossilDescriptions = EnumExtensions.GetAllDescriptions<FossilEnum>();
        var fossilSet = new HashSet<string>(fossilDescriptions, StringComparer.OrdinalIgnoreCase);
        
        return items.Where(item => 
            fossilSet.Any(description => item.BaseType.Contains(description))
        ).ToList();
    }
    
    private List<Item> FilterForScarabs(List<Item> items)
    {
        var scarabDescriptions = EnumExtensions.GetAllDescriptions<ScarabEnum>();
        var scarabSet = new HashSet<string>(scarabDescriptions, StringComparer.OrdinalIgnoreCase);
        
        return items.Where(item => 
            scarabSet.Any(description => item.BaseType.Contains(description))
        ).ToList();
    }
    
    private List<Item> FilterForOils(List<Item> items)
    {
        var oilDescriptions = EnumExtensions.GetAllDescriptions<OilEnum>();
        var oilSet = new HashSet<string>(oilDescriptions, StringComparer.OrdinalIgnoreCase);
        
        return items.Where(item => 
            oilSet.Any(description => item.BaseType.Contains(description))
        ).ToList();
    }

    private void ProcessEssenceItems(List<Item> essenceItems, Stash stash)
    {
        _tradeListHandler.SetEssenceMessage(essenceItems, stash);
    }

    private void ProcessFossilItems(List<Item> fossilItems, Stash stash)
    {
        _tradeListHandler.SetFossilMessage(fossilItems, stash);
    }
    
    private void ProcessScarabItems(List<Item> scarabItems, Stash stash)
    {
        _tradeListHandler.SetScarabMessage(scarabItems, stash);
    }
    
    private void ProcessOilItems(List<Item> scarabItems, Stash stash)
    {
        _tradeListHandler.SetOilMessage(scarabItems, stash);
    }




}
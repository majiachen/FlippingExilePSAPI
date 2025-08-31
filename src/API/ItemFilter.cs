using FlippingExilesPublicStashAPI.PublicStashPOCO;
using FlippingExilesPublicStashAPI.Redis;

public class ItemFilter
{
    private readonly ILogger<ItemFilter> _logger;
    private readonly TradeListHandler _tradeListHandler;

    public ItemFilter(ILogger<ItemFilter> logger, TradeListHandler tradeListHandler)
    {
        _logger = logger;
        _tradeListHandler = tradeListHandler;
    }

    public async Task ProcessItems(List<Stash> stashes)
    {
        if (stashes == null) return;

        await ProcessFilteredStashes(stashes.Where(stash =>
                stash.Items is { Count: > 0 } &&
                stash.StashType != null &&
                POCOHelper.LeaguesList.Any(league => league.Id.Equals(stash.League)) &&
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
                       item.BaseType.Contains("Remnant of Corruption", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Orb", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Oil", StringComparison.CurrentCultureIgnoreCase) ||
                       item.BaseType.Contains("Delirium Orb", StringComparison.CurrentCultureIgnoreCase) ||
                       item.Properties?.Any(p => p.Type is 32) == true)))))
            .ToList());
    }

    public async Task ProcessFilteredStashes(List<Stash> stashes)
    {
        if (stashes == null) return;

        foreach (var stash in stashes)
        {
            await _tradeListHandler.RemoveStashIfExist(stash.Id);
            // First filter for items with notes (creates a new list, doesn't modify original)
            var itemsWithNotes = FilterForItemsWithNotes(stash.Items);
            if (itemsWithNotes.Count > 0)
            {
                _logger.LogInformation("item with notes: " + string.Join(",",
                    itemsWithNotes.Select(i => $"name: {i.Name} + basetype : {i.BaseType} + price : {i.Note}")));
                List<Item> essenceItems = new();
                List<Item> fossilItems = new();
                List<Item> scarabItems = new();
                List<Item> oilItems = new();

                // Apply separate filters to the filtered list
                if (stash.StashType.Contains("Essence", StringComparison.CurrentCultureIgnoreCase))
                {
                    essenceItems = FilterForEssences(itemsWithNotes);
                }
                else if (stash.StashType.Contains("Blight", StringComparison.CurrentCultureIgnoreCase))
                {
                    oilItems = FilterForOils(itemsWithNotes);
                }
                else if (stash.StashType.Contains("Fragment", StringComparison.CurrentCultureIgnoreCase))
                {
                    scarabItems = FilterForScarabs(itemsWithNotes);
                }
                else if (stash.StashType.Contains("Delve", StringComparison.CurrentCultureIgnoreCase))
                {
                    fossilItems = FilterForFossils(itemsWithNotes);
                }
                else if (stash.StashType.Contains("PremiumStash", StringComparison.CurrentCultureIgnoreCase))
                {
                    // For PremiumStash, we need to check the items themselves to determine what type they are
                    essenceItems = FilterForEssences(itemsWithNotes);
                    fossilItems = FilterForFossils(itemsWithNotes);
                    scarabItems = FilterForScarabs(itemsWithNotes);
                    oilItems = FilterForOils(itemsWithNotes);
                }

                // Process each filtered list separately
                if (essenceItems.Any()) ProcessEssenceItems(essenceItems, stash);
                if (fossilItems.Any()) ProcessFossilItems(fossilItems, stash);
                if (scarabItems.Any()) ProcessScarabItems(scarabItems, stash);
                if (oilItems.Any()) ProcessOilItems(oilItems, stash);
            }
        }
    }

    private List<Item> FilterForItemsWithNotes(List<Item> items)
    {
        return items?.Where(item => !string.IsNullOrEmpty(item.Note))?.ToList() ?? new List<Item>();
    }

    private List<Item> FilterForEssences(List<Item> items)
    {
        var essenceDescriptions = POCOHelper.GetAllDescriptions<EssenceEnum>();

        return items.Where(item =>
            essenceDescriptions.Any(description =>
                item.BaseType.Contains(description, StringComparison.CurrentCultureIgnoreCase))
        ).ToList();
    }

    private List<Item> FilterForFossils(List<Item> items)
    {
        var fossilDescriptions = POCOHelper.GetAllDescriptions<FossilEnum>();

        return items.Where(item =>
            fossilDescriptions.Any(description =>
                item.BaseType.Contains(description, StringComparison.CurrentCultureIgnoreCase))
        ).ToList();
    }

    private List<Item> FilterForScarabs(List<Item> items)
    {
        var scarabDescriptions = POCOHelper.GetAllDescriptions<ScarabEnum>();

        return items.Where(item =>
            scarabDescriptions.Any(description =>
                item.BaseType.Contains(description, StringComparison.CurrentCultureIgnoreCase))
        ).ToList();
    }

    private List<Item> FilterForOils(List<Item> items)
    {
        var oilDescriptions = POCOHelper.GetAllDescriptions<OilEnum>();

        return items.Where(item =>
            oilDescriptions.Any(description =>
                item.BaseType.Contains(description, StringComparison.CurrentCultureIgnoreCase))
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
using System.Text.RegularExpressions;
using FlippingExilesPublicStashAPI.PublicStashPOCO;
using Newtonsoft.Json;

namespace FlippingExilesPublicStashAPI.Redis;

public class TradeListHandler
{
    private readonly ILogger<TradeListHandler> _logger;
    private readonly RedisMessage _redisMessage;

    public TradeListHandler(ILogger<TradeListHandler> logger, RedisMessage redisMessage)
    {
        _logger = logger;
        _redisMessage = redisMessage;
    }

    public async Task SetEssenceMessage(List<Item> essenceItems, Stash stash)
    {
        await ProcessItems(essenceItems, stash, POCOHelper.EssenceEnumsList);
    }

    public async Task SetFossilMessage(List<Item> fossilItems, Stash stash)
    {
        await ProcessItems(fossilItems, stash, POCOHelper.FossilEnumsList);
    }

    public async Task SetScarabMessage(List<Item> scarabItems, Stash stash)
    {
        await ProcessItems(scarabItems, stash, POCOHelper.ScarabEnumsList);
    }

    public async Task SetOilMessage(List<Item> scarabItems, Stash stash)
    {
        await ProcessItems(scarabItems, stash, POCOHelper.OilEnumsList);
    }

    private async Task ProcessItems(List<Item> items, Stash stash, IEnumerable<Enum> enumList)
    {
        var enumValues = enumList.ToList();
        var category = GetCategoryFromEnumList(enumValues);
        var currencySuffixList = POCOHelper.GetAllDescriptions<CurrencySuffixEnum>();

        foreach (var enumValue in enumValues)
        {
            var enumDescription = enumValue.GetDescription();
            var slug = Slugify(enumDescription);
            var typeKey = $"type:{category}:{slug}";

            var filteredItems = items.Where(item =>
                item.Note != null &&
                (item.TypeLine?.Contains(enumDescription, StringComparison.OrdinalIgnoreCase) == true ||
                 item.BaseType?.Contains(enumDescription, StringComparison.OrdinalIgnoreCase) == true) &&
                currencySuffixList.Any(suffix =>
                    item.Note.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            ).ToList();


            if (filteredItems.Count == 0) continue;

            _logger.LogInformation($"Found {filteredItems.Count} {enumDescription} items in stash {stash.Id}");

            currencySuffixList.ForEach(async suffix =>
                await RemoveStashItemsFromTypeAsync(stash.Id, typeKey + $":{suffix}"));


            // 2. Add new items
            for (var i = 0; i < filteredItems.Count; i++)
            {
                var item = filteredItems[i];
                var fieldName = $"stash:{stash.Id}:{i}";

                var itemData = new
                {
                    item.Note,
                    item.StackSize,
                    stash.AccountName,
                    stash.League,
                    LastUpdated = DateTime.UtcNow,
                    item.BaseType,
                    item.TypeLine
                };
                var priceDenomination = currencySuffixList.FirstOrDefault(suffix =>
                    item.Note.Contains(suffix, StringComparison.OrdinalIgnoreCase));
                var typeKeyWithCurrency = typeKey + $":{priceDenomination}";
                var itemJson = JsonConvert.SerializeObject(itemData);
                await _redisMessage.HashSetAsync(typeKeyWithCurrency, fieldName, itemJson);


                // 3. Update counters and indexes
                await _redisMessage.HashSetAsync($"{typeKeyWithCurrency}:count", $"stash:{stash.Id}",
                    filteredItems.Count.ToString());
                await _redisMessage.SetAddAsync("item:types", typeKeyWithCurrency);
                await _redisMessage.SetAddAsync($"stash:{stash.Id}:types", typeKeyWithCurrency);

                _logger.LogInformation(
                    $"Added {filteredItems.Count} items to {typeKeyWithCurrency} for stash {stash.Id}");
            }
        }
    }

    public async Task RemoveStashIfExist(string stashId)
    {
        var stashTypeSetLength = await _redisMessage.SetLengthAsync($"stash:{stashId}:types");
        if (stashTypeSetLength > 0)
        {
            var stashTypes = await _redisMessage.SetMembersAsync($"stash:{stashId}:types");
            foreach (var type in stashTypes) await RemoveStashItemsFromTypeAsync(stashId, type.ToString());
            await _redisMessage.KeyDeleteAsync($"stash:{stashId}:types");
        }
    }


    private async Task RemoveStashItemsFromTypeAsync(string stashId, string typeKey)
    {
        // Get all fields for this stash in the type hash
        var allFields = await _redisMessage.HashKeysAsync(typeKey);
        var stashFields = allFields.Where(f => f.ToString().StartsWith($"stash:{stashId}:"));

        if (stashFields.Any())
            await _redisMessage.HashDeleteAsync(typeKey, stashFields.Select(f => f.ToString()).ToArray());

        await _redisMessage.HashDeleteAsync($"{typeKey}:count", $"stash:{stashId}");
    }

    private static string GetCategoryFromEnumList(IEnumerable<Enum> enumList)
    {
        if (enumList.SequenceEqual(POCOHelper.FossilEnumsList)) return "fossil";
        if (enumList.SequenceEqual(POCOHelper.EssenceEnumsList)) return "essence";
        if (enumList.SequenceEqual(POCOHelper.ScarabEnumsList)) return "scarab";
        if (enumList.SequenceEqual(POCOHelper.OilEnumsList)) return "oil";
        return "unknown";
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.ToLowerInvariant();
        // Replace any non-alphanumeric with underscore
        var slug = Regex.Replace(lower, "[^a-z0-9]+", "_");
        // Trim underscores
        slug = slug.Trim('_');
        return slug;
    }

    // Keeping the old methods in case of future use but currently not used
    private List<AccountAndStashMap> GetExistingRedisData(string redisMessageKey)
    {
        var listOfAccountAndStashMapFromRedis = new List<AccountAndStashMap>();
        var redisMessage = _redisMessage.GetMessage(redisMessageKey);
        if (redisMessage != null && redisMessage.Length > 0)
            listOfAccountAndStashMapFromRedis = JsonConvert.DeserializeObject<List<AccountAndStashMap>>(
                _redisMessage.GetMessage(redisMessageKey));
        return listOfAccountAndStashMapFromRedis;
    }

    private string ConfigureRedisKey(string enumDescription, string currencySuffix, IEnumerable<Enum> enumList,
        Stash stash)
    {
        string redisKey;
        if (enumList.SequenceEqual(POCOHelper.FossilEnumsList))
            redisKey = RedisMessageKeyHelper.GetFossilTradeListRedisKey();
        else if (enumList.SequenceEqual(POCOHelper.EssenceEnumsList))
            redisKey = RedisMessageKeyHelper.GetEssenceTradeListRedisKey();
        else if (enumList.SequenceEqual(POCOHelper.ScarabEnumsList))
            redisKey = RedisMessageKeyHelper.GetScarabTradeListRedisKey();
        else if (enumList.SequenceEqual(POCOHelper.OilEnumsList))
            redisKey = RedisMessageKeyHelper.GetOilsTradeListRedisKey();
        else
            redisKey = RedisMessageKeyHelper.GetUnkownTradeListRedisKey();
        return redisKey + "." + stash.League + "." + enumDescription.Replace(" ", "-") + "." + currencySuffix;
    }
}
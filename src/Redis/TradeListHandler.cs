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

    public void SetEssenceMessage(List<Item> essenceItems, Stash stash)
    {
        ProcessItems(essenceItems, stash, EnumExtensions.EssenceEnumsList);
    }

    public void SetFossilMessage(List<Item> fossilItems, Stash stash)
    {
        ProcessItems(fossilItems, stash, EnumExtensions.FossilEnumsList);
    }
    
    public void SetScarabMessage(List<Item> scarabItems, Stash stash)
    {
        ProcessItems(scarabItems, stash, EnumExtensions.ScarabEnumsList);
    }
    
    public void SetOilMessage(List<Item> scarabItems, Stash stash)
    {
        ProcessItems(scarabItems, stash, EnumExtensions.OilEnumsList);
    }

    private void ProcessItems(List<Item> items, Stash stash, IEnumerable<Enum> enumList)
    {
        // Get the account and stash information
        var accountAndStashMap = new AccountAndStashMap
        {
            AccountName = stash.AccountName,
            StashId = stash.Id
        };
        List<string> currencySuffixList = EnumExtensions.GetAllDescriptions<CurrencySuffixEnum>();

        var enumValues = enumList.ToList();
        foreach (var enumValue in enumValues)
        {
            // Get the description of the current enum value
            var enumDescription = enumValue.GetDescription();
            
            // Filter items that have BaseType matching the enum description
            var filteredItems = items.Where(item => 
                item.Note != null && 
                item.BaseType != null &&
                item.BaseType.Contains(enumDescription, StringComparison.CurrentCultureIgnoreCase)
            ).ToList();

            // Process the filtered items for this specific enum value
            if (filteredItems.Count == 0) continue;
            
            // For each item, find which currency suffix it contains and create a key accordingly
            foreach (var item in filteredItems)
            {
                var matchingCurrencySuffix = currencySuffixList.FirstOrDefault(suffix => 
                    item.Note.Contains(suffix, StringComparison.CurrentCultureIgnoreCase));
            
                if (matchingCurrencySuffix == null) continue;
            
                _logger.LogInformation("Item found, processing then adding to list: " + string.Join(";",filteredItems.Select(item => item.ToString())));

                accountAndStashMap.Item = new List<Item> { item };
            
                var redisMessageKey = ConfigureRedisKey(enumValue.GetDescription(), matchingCurrencySuffix, enumValues, stash);
                var listOfAccountAndStashMapFromRedis = GetExistingRedisData(redisMessageKey);
            
                listOfAccountAndStashMapFromRedis.RemoveAll(map => map.StashId == stash.Id);
            
                listOfAccountAndStashMapFromRedis.Add(accountAndStashMap);
            
                _redisMessage.SetMessage(redisMessageKey, JsonConvert.SerializeObject(listOfAccountAndStashMapFromRedis));
                _logger.LogInformation("Trade list updated in redis: " + listOfAccountAndStashMapFromRedis);
            }
        }
    }

    private List<AccountAndStashMap> GetExistingRedisData(string redisMessageKey)
    {
        var listOfAccountAndStashMapFromRedis = new List<AccountAndStashMap>();
        var redisMessage = _redisMessage.GetMessage(redisMessageKey);
        if (redisMessage != null && redisMessage.Length > 0)
        {
            listOfAccountAndStashMapFromRedis = JsonConvert.DeserializeObject<List<AccountAndStashMap>>(
                _redisMessage.GetMessage(redisMessageKey));
        }
        return listOfAccountAndStashMapFromRedis;
    }

    private string ConfigureRedisKey(string enumDescription, string currencySuffix, IEnumerable<Enum> enumList,
        Stash stash)
    {
        string redisKey;
        
        // Determine which Redis key to use based on the enum list type
        if (enumList.SequenceEqual(EnumExtensions.FossilEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetFossilTradeListRedisKey();
        }else if (enumList.SequenceEqual(EnumExtensions.EssenceEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetEssenceTradeListRedisKey();
        }else if (enumList.SequenceEqual(EnumExtensions.ScarabEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetScarabTradeListRedisKey();
        }else if (enumList.SequenceEqual(EnumExtensions.OilEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetOilsTradeListRedisKey();
        }else
        {
            // Default to essence key if unknown type
            redisKey = RedisMessageKeyHelper.GetUnkownTradeListRedisKey();
        }
        
        return redisKey + "."+stash.League+"." + enumDescription.Replace(" ", "-") + "." + currencySuffix;
    }
        
}
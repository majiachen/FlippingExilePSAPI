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

    private void ProcessItems(List<Item> items, Stash stash, IEnumerable<Enum> enumList)
    {
        // Get the account and stash information
        var accountAndStashMap = new AccountAndStashMap
        {
            AccountName = stash.AccountName,
            StashId = stash.Id
        };
        List<string> currencySuffixList = EnumExtensions.GetAllDescriptions<CurrencySuffixEnum>();

        foreach (var enumValue in enumList)
        {
            foreach (var currencySuffix in EnumExtensions.CurrencySuffixEnumsList)
            {
                // Filter items that contain the current currency suffix
                var filteredItems = items.Where(item => 
                    item.Note != null && 
                    item.Note.Contains(currencySuffix.GetDescription()) && item.Name.Contains(enumValue.GetDescription())
                ).ToList();

                // Process the filtered items for this specific currency suffix
                if (filteredItems.Count == 0) continue;
                _logger.LogInformation("Item found, processing then adding to list: " + string.Join(";",items.ToString()));

                accountAndStashMap.Item = filteredItems;
                
                var redisMessageKey = ConfigureRedisKey(enumValue.GetDescription(), currencySuffix.GetDescription(), enumList, stash);
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
        if (Equals(enumList, EnumExtensions.FossilEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetFossilTradeListRedisKey();
        }else if (Equals(enumList, EnumExtensions.EssenceEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetEssenceTradeListRedisKey();
        }else if (Equals(enumList, EnumExtensions.ScarabEnumsList))
        {
            redisKey = RedisMessageKeyHelper.GetScarabTradeListRedisKey();
        }else
        {
            // Default to essence key if unknown type
            redisKey = RedisMessageKeyHelper.GetUnkownTradeListRedisKey();
        }
        
        return redisKey + "."+stash.League+"." + enumDescription.Replace(" ", "-") + "." + currencySuffix;
    }
        
}
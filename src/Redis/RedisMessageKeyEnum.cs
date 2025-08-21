// ReSharper disable IdentifierTypo

using FlippingExilesPublicStashAPI.PublicStashPOCO;

namespace FlippingExilesPublicStashAPI.Redis;
using System.ComponentModel;
using System.Reflection;

public enum RedisMessageKeyEnum
{
    [Description("FlippingExilesPublicStashAPI.test")]
    Test,
    
    [Description("FlippingExilesPublicStashAPI.EssenceTradeList")]
    EssenceTradeList,
    
    [Description("FlippingExilesPublicStashAPI.FossilTradeList")]
    FossilTradeList,
    
    [Description("FlippingExilesPublicStashAPI.CatalystsTradeList")]
    CatalystsTradeList,
    
    [Description("FlippingExilesPublicStashAPI.OilsTradeList")]
    OilsTradeList,
    
    [Description("FlippingExilesPublicStashAPI.DeliriumOrbsTradeList")]
    DeliriumOrbsTradeList,
    
    [Description("FlippingExilesPublicStashAPI.BreachstonesTradeList")]
    BreachstonesTradeList,
    
    [Description("FlippingExilesPublicStashAPI.EmblemsTradeList")]
    EmblemsTradeList,
    
    [Description("FlippingExilesPublicStashAPI.ChangeId")]
    ChangeId,

    [Description("FlippingExilesPublicStashAPI.LeagueName")]
    LeagueName,
    
}

public static class RedisMessageKeyHelper
{
    
    public static string GetTestRedisKey() => GetDescription(RedisMessageKeyEnum.Test);
    public static string GetEssenceTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.EssenceTradeList);
    public static string GetFossilTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.FossilTradeList);
    public static string GetCatalystsTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.CatalystsTradeList);
    public static string GetOilsTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.OilsTradeList);
    public static string GetDeliriumOrbsTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.DeliriumOrbsTradeList);
    public static string GetBreachstonesTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.BreachstonesTradeList);
    public static string GetEmblemsTradeListRedisKey() => GetDescription(RedisMessageKeyEnum.EmblemsTradeList);
    public static string GetChangeIdRedisKey() => GetDescription(RedisMessageKeyEnum.ChangeId);
    public static string GetLeagueNameRedisKey() => GetDescription(RedisMessageKeyEnum.LeagueName);

    

    private static string GetDescription(RedisMessageKeyEnum key)
    {
        var field = key.GetType().GetField(key.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? key.ToString();
    }
    
    
    public static string GetDescriptionWithHyphens(Enum value)
    {
        string description = value.GetDescription();
        return description.Replace(" ", "-");
    }

}
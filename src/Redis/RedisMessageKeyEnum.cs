// ReSharper disable IdentifierTypo
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
    
    public static string GetTestDescription() => GetDescription(RedisMessageKeyEnum.Test);
    public static string GetEssenceTradeListDescription() => GetDescription(RedisMessageKeyEnum.EssenceTradeList);
    public static string GetFossilTradeListDescription() => GetDescription(RedisMessageKeyEnum.FossilTradeList);
    public static string GetCatalystsTradeListDescription() => GetDescription(RedisMessageKeyEnum.CatalystsTradeList);
    public static string GetOilsTradeListDescription() => GetDescription(RedisMessageKeyEnum.OilsTradeList);
    public static string GetDeliriumOrbsTradeListDescription() => GetDescription(RedisMessageKeyEnum.DeliriumOrbsTradeList);
    public static string GetBreachstonesTradeListDescription() => GetDescription(RedisMessageKeyEnum.BreachstonesTradeList);
    public static string GetEmblemsTradeListDescription() => GetDescription(RedisMessageKeyEnum.EmblemsTradeList);
    public static string GetChangeIdDescription() => GetDescription(RedisMessageKeyEnum.ChangeId);
    public static string GetLeagueNameDescription() => GetDescription(RedisMessageKeyEnum.LeagueName);
    

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
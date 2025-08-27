using System.ComponentModel;
using System.Reflection;
using FlippingExilesPublicStashAPI.Redis;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public static class POCOHelper
{
    public static List<League> LeaguesList;
    public static LeagueMarketData MarketData;
    
    public static IEnumerable<Enum> EssenceEnumsList => Enum.GetValues(typeof(EssenceEnum)).Cast<Enum>();

    public static IEnumerable<Enum> FossilEnumsList => Enum.GetValues(typeof(FossilEnum)).Cast<Enum>();
    
    public static IEnumerable<Enum> ScarabEnumsList => Enum.GetValues(typeof(ScarabEnum)).Cast<Enum>();
    
    public static IEnumerable<Enum> OilEnumsList => Enum.GetValues(typeof(OilEnum)).Cast<Enum>();

    public static readonly CurrencySuffixEnum[] CurrencySuffixEnumsList = (CurrencySuffixEnum[])Enum.GetValues(typeof(CurrencySuffixEnum));

    public static string GetDescription(this Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
    
    public static List<string> GetAllDescriptions<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(v => GetDescription(v))
            .ToList();
    }
    
    public static T GetValueByDescription<T>(string description) where T : Enum
    {
        foreach (T value in Enum.GetValues(typeof(T)))
        {
            if (value.GetDescription() == description)
            {
                return value;
            }
        }
        throw new ArgumentException($"No enum value of type {typeof(T).Name} found with description '{description}'");
    }
}
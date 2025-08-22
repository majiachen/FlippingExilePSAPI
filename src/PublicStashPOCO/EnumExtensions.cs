using System.ComponentModel;
using System.Reflection;
using FlippingExilesPublicStashAPI.LeaguePOCO;
using FlippingExilesPublicStashAPI.Redis;

namespace FlippingExilesPublicStashAPI.PublicStashPOCO;

public static class EnumExtensions
{
    
    public static IEnumerable<Enum> EssenceEnumsList => Enum.GetValues(typeof(EssenceEnum)).Cast<Enum>();

    public static IEnumerable<Enum> FossilEnumsList => Enum.GetValues(typeof(FossilEnum)).Cast<Enum>();

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
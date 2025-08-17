using System.ComponentModel;

public enum EssenceEnum
{
    // Shrieking Essences
    [Description("Shrieking Essence Of Anger")]
    ShriekingEssenceOfAnger,
    [Description("Shrieking Essence Of Anguish")]
    ShriekingEssenceOfAnguish,
    [Description("Shrieking Essence Of Contempt")]
    ShriekingEssenceOfContempt,
    [Description("Shrieking Essence Of Doubt")]
    ShriekingEssenceOfDoubt,
    [Description("Shrieking Essence Of Dread")]
    ShriekingEssenceOfDread,
    [Description("Shrieking Essence Of Envy")]
    ShriekingEssenceOfEnvy,
    [Description("Shrieking Essence Of Fear")]
    ShriekingEssenceOfFear,
    [Description("Shrieking Essence Of Greed")]
    ShriekingEssenceOfGreed,
    [Description("Shrieking Essence Of Hatred")]
    ShriekingEssenceOfHatred,
    [Description("Shrieking Essence Of Loathing")]
    ShriekingEssenceOfLoathing,
    [Description("Shrieking Essence Of Misery")]
    ShriekingEssenceOfMisery,
    [Description("Shrieking Essence Of Rage")]
    ShriekingEssenceOfRage,
    [Description("Shrieking Essence Of Scorn")]
    ShriekingEssenceOfScorn,
    [Description("Shrieking Essence Of Sorrow")]
    ShriekingEssenceOfSorrow,
    [Description("Shrieking Essence Of Spite")]
    ShriekingEssenceOfSpite,
    [Description("Shrieking Essence Of Suffering")]
    ShriekingEssenceOfSuffering,
    [Description("Shrieking Essence Of Torment")]
    ShriekingEssenceOfTorment,
    [Description("Shrieking Essence Of Woe")]
    ShriekingEssenceOfWoe,
    [Description("Shrieking Essence Of Wrath")]
    ShriekingEssenceOfWrath,

    // Deafening Essences
    [Description("Deafening Essence Of Anger")]
    DeafeningEssenceOfAnger,
    [Description("Deafening Essence Of Anguish")]
    DeafeningEssenceOfAnguish,
    [Description("Deafening Essence Of Contempt")]
    DeafeningEssenceOfContempt,
    [Description("Deafening Essence Of Doubt")]
    DeafeningEssenceOfDoubt,
    [Description("Deafening Essence Of Dread")]
    DeafeningEssenceOfDread,
    [Description("Deafening Essence Of Envy")]
    DeafeningEssenceOfEnvy,
    [Description("Deafening Essence Of Fear")]
    DeafeningEssenceOfFear,
    [Description("Deafening Essence Of Greed")]
    DeafeningEssenceOfGreed,
    [Description("Deafening Essence Of Hatred")]
    DeafeningEssenceOfHatred,
    [Description("Deafening Essence Of Loathing")]
    DeafeningEssenceOfLoathing,
    [Description("Deafening Essence Of Misery")]
    DeafeningEssenceOfMisery,
    [Description("Deafening Essence Of Rage")]
    DeafeningEssenceOfRage,
    [Description("Deafening Essence Of Scorn")]
    DeafeningEssenceOfScorn,
    [Description("Deafening Essence Of Sorrow")]
    DeafeningEssenceOfSorrow,
    [Description("Deafening Essence Of Spite")]
    DeafeningEssenceOfSpite,
    [Description("Deafening Essence Of Suffering")]
    DeafeningEssenceOfSuffering,
    [Description("Deafening Essence Of Torment")]
    DeafeningEssenceOfTorment,
    [Description("Deafening Essence Of Woe")]
    DeafeningEssenceOfWoe,
    [Description("Deafening Essence Of Wrath")]
    DeafeningEssenceOfWrath,

    // Special Essences
    [Description("Essence Of Hysteria")]
    EssenceOfHysteria,
    [Description("Essence Of Delirium")]
    EssenceOfDelirium,
    [Description("Essence Of Horror")]
    EssenceOfHorror,
    [Description("Essence Of Insanity")]
    EssenceOfInsanity,
    [Description("Remnant Of Corruption")]
    RemnantOfCorruption
}

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }
}
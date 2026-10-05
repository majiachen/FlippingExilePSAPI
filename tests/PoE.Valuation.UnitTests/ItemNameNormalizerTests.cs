using PoE.Valuation.Core.Validation;

namespace PoE.Valuation.UnitTests;

public class ItemNameNormalizerTests
{
    [Theory]
    [InlineData("Chaos Orb", "chaos orb")]
    [InlineData("  Chaos   Orb  ", "chaos orb")]
    [InlineData("Chaos\tOrb", "chaos orb")]
    [InlineData("Chaos-Orb", "chaos orb")]
    [InlineData("Jeweller's Orb", "jewellers orb")]
    [InlineData("Jewellers Orb", "jewellers orb")]
    [InlineData("Kaom's Heart", "kaoms heart")]
    [InlineData("Vaal Regalia (Level 60)", "vaal regalia level 60")]
    [InlineData("Orb of Fusing.", "orb of fusing")]
    [InlineData("Chaos Orb/Fragment", "chaos orb fragment")]
    [InlineData("Metadata/Items/Currency/CurrencyRerollRare", "metadata items currency currencyrerollrare")]

    public void Normalize_CanonicalizesSeparatorsAndCase(string input, string expected)
    {
        Assert.Equal(expected, ItemNameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("'''")]
    [InlineData("///")]
    public void Normalize_ReturnsEmpty_WhenNothingIsLeft(string? input)
    {
        Assert.Equal(string.Empty, ItemNameNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_IsStableSoCatalogueAndQueryKeysMatch()
    {
        // The whole lookup depends on both sides producing the same key for the same item.
        Assert.Equal(
            ItemNameNormalizer.Normalize("Jeweller's Orb"),
            ItemNameNormalizer.Normalize("jewellers  orb"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("chaos\torb:x")] // control character and ':' are rejected
    [InlineData("chaos:orb")]

    public void TryValidateItemName_RejectsBlankOrMalformed(string? name)
    {
        Assert.False(ApiInputValidator.TryValidateItemName(name, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateItemName_RejectsOverlongName()
    {
        var name = new string('a', ItemNameNormalizer.MaxItemNameLength + 1);

        Assert.False(ApiInputValidator.TryValidateItemName(name, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("Chaos Orb")]
    [InlineData("Jeweller's Orb")]
    [InlineData("Vaal Regalia (Level 60)")]
    [InlineData("Metadata/Items/Currency/CurrencyRerollRare")]
    public void TryValidateItemName_AcceptsNormalNames(string name)
    {
        Assert.True(ApiInputValidator.TryValidateItemName(name, out var error));
        Assert.Null(error);
    }
}

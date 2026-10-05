using PoE.Valuation.Core.Validation;

namespace PoE.Valuation.UnitTests;

/// <summary>
/// League-name validation for the /api surface. PoE league ids are the league name itself, so the
/// validator must accept the punctuation real league names contain — the previous alphanumeric-only
/// rule rejected valid leagues such as <c>Solo Self-Found</c>.
/// </summary>
public class ApiInputValidatorTests
{
    [Theory]
    [InlineData("Standard")]
    [InlineData("Hardcore")]
    [InlineData("CSS9Standard")]
    [InlineData("Solo Self-Found")]
    [InlineData("Hardcore SSF")]
    [InlineData("SSF R Allflame")]
    [InlineData("Limey Whelps (PL86569)")]
    [InlineData("Oath_of_the_Wild")]
    [InlineData("Kaom's League")]
    [InlineData("Season 1.2")]
    public void TryValidateLeagueName_AcceptsRealPoELeagueNames(string league)
    {
        Assert.True(ApiInputValidator.TryValidateLeagueName(league, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Standard<script>")]
    [InlineData("Standard?id=1")]
    [InlineData("a:b")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    [InlineData("Standard/../etc")]
    [InlineData("Standard|pipe")]
    public void TryValidateLeagueName_RejectsMalformedLeagueNames(string? league)
    {
        Assert.False(ApiInputValidator.TryValidateLeagueName(league, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateLeagueName_RejectsOverlongLeagueName()
    {
        var league = new string('a', ApiInputValidator.MaxLeagueNameLength + 1);

        Assert.False(ApiInputValidator.TryValidateLeagueName(league, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("Metadata/Items/Currency/CurrencyRerollRare")]
    [InlineData("item-a")]
    [InlineData("Metadata/Items/Currency/CurrencyRerollRare/")]
    public void TryValidateItemId_StillAcceptsMetadataPaths(string itemId)
    {
        Assert.True(ApiInputValidator.TryValidateItemId(itemId, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("Metadata/Items/Currency/Currency Reroll Rare")] // whitespace is not part of an item id
    [InlineData("a:b")]
    [InlineData("")]
    public void TryValidateItemId_RejectsWhitespaceAndColon(string itemId)
    {
        Assert.False(ApiInputValidator.TryValidateItemId(itemId, out var error));
        Assert.NotNull(error);
    }
}

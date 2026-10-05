using PoE.Valuation.Core.Data;
using PoE.Valuation.Core.Validation;

namespace PoE.Valuation.UnitTests;

public class ItemNameMatcherTests
{
    private const string Chaos = "Metadata/Items/Currency/CurrencyRerollRare";
    private const string Divine = "Metadata/Items/Currency/CurrencyModValues";
    private const string Alch = "Metadata/Items/Currency/CurrencyUpgradeToRare";

    private static ItemMetadataEntry Entry(string metadata, string name, string alias) =>
        new(metadata, name, "Currency", alias, ItemNameNormalizer.Normalize(name));

    private static IReadOnlyList<ItemMetadataEntry> Catalogue() => new[]
    {
        Entry(Chaos, "Chaos Orb", "chaos"),
        Entry(Divine, "Divine Orb", "divine"),
        Entry(Alch, "Orb of Alchemy", "alch"),
    };

    [Fact]
    public void Rank_ExactMatchWinsAndResolvesToMetadataPath()
    {
        var matches = ItemNameMatcher.Rank("Chaos Orb", Catalogue(), 10);

        Assert.Single(matches);
        Assert.Equal(Chaos, matches[0].Metadata);
        Assert.Equal(ItemMatchKind.Exact, matches[0].Kind);
    }

    [Fact]
    public void Rank_NormalizesTheQuerySoSpellingDoesNotMatter()
    {
        var matches = ItemNameMatcher.Rank("  chaos   orb ", Catalogue(), 10);

        Assert.Single(matches);
        Assert.Equal(Chaos, matches[0].Metadata);
        Assert.Equal(ItemMatchKind.Exact, matches[0].Kind);
    }

    [Fact]
    public void Rank_AcceptsTradeAliasAsAQuery()
    {
        var matches = ItemNameMatcher.Rank("alch", Catalogue(), 10);

        Assert.Equal(Alch, matches[0].Metadata);
        Assert.Equal(ItemMatchKind.Alias, matches[0].Kind);
    }

    [Fact]
    public void Rank_PrefixMatchResolvesTheItem()
    {
        var matches = ItemNameMatcher.Rank("chaos or", Catalogue(), 10);

        Assert.Single(matches);
        Assert.Equal(Chaos, matches[0].Metadata);
        Assert.Equal(ItemMatchKind.Prefix, matches[0].Kind);
    }

    [Fact]
    public void Rank_ContainsMatchRanksBelowPrefix()
    {
        var catalogue = new[]
        {
            Entry(Chaos, "Chaos Orb", "chaos"),
            Entry("Metadata/Items/Currency/ChaosFragment", "Orb Chaos Fragment", null),
        };

        var matches = ItemNameMatcher.Rank("chaos frag", catalogue, 10);

        Assert.Single(matches);
        Assert.Equal("Metadata/Items/Currency/ChaosFragment", matches[0].Metadata);
        Assert.Equal(ItemMatchKind.Contains, matches[0].Kind);
    }


    [Fact]
    public void Rank_OrdersByKindThenShortestNameThenMetadataPath()
    {
        var catalogue = new[]
        {
            Entry(Chaos, "Chaos Orb", "chaos"),
            Entry("Metadata/Items/Currency/ChaosFragment", "Chaos Orb Fragment", null),
            Entry("Metadata/Items/Currency/AAChaosShard", "AA Chaos Shard", null),
        };

        var matches = ItemNameMatcher.Rank("chaos", catalogue, 10);

        Assert.Equal(3, matches.Count);
        Assert.Equal(ItemMatchKind.Alias, matches[0].Kind);
        Assert.Equal(Chaos, matches[0].Metadata);
        // Both remaining are prefixes; the shorter lookup key ranks first.
        Assert.Equal("Metadata/Items/Currency/ChaosFragment", matches[1].Metadata);
        Assert.Equal("Metadata/Items/Currency/AAChaosShard", matches[2].Metadata);
    }

    [Fact]
    public void Rank_TruncatesToLimit()
    {
        var matches = ItemNameMatcher.Rank("orb", Catalogue(), 2);

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Rank_DropsNonMatchingCandidates()
    {
        var matches = ItemNameMatcher.Rank("exalted", Catalogue(), 10);

        Assert.Empty(matches);
    }

    [Fact]
    public void Rank_ZeroLimitReturnsNothing()
    {
        Assert.Empty(ItemNameMatcher.Rank("chaos orb", Catalogue(), 0));
    }

    [Fact]
    public void Classify_MetadataPathQueryMatchesOnlyThatPath()
    {
        var entry = Entry(Chaos, "Chaos Orb", "chaos");

        Assert.Equal(ItemMatchKind.Exact, ItemNameMatcher.Classify(Chaos, entry));
        Assert.Equal(ItemMatchKind.None, ItemNameMatcher.Classify(Divine, entry));
    }

    [Theory]
    [InlineData("Metadata/Items/Currency/CurrencyRerollRare", true)]
    [InlineData("metadata/items/X", true)]
    [InlineData("Chaos Orb", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMetadataPath_DetectsTheLongForm(string? value, bool expected)
    {
        Assert.Equal(expected, ItemNameMatcher.IsMetadataPath(value));
    }
}

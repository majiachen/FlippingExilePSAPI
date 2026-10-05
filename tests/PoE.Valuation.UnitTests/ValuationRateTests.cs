using System.Collections.Generic;
using PoE.Valuation.Core.Data;

namespace PoE.Valuation.UnitTests;

/// <summary>
/// Rate math over the ratio dictionaries the currency-exchange API publishes. The live payload orients
/// each pair arbitrarily, so the labels are not a reliable ordering — these tests pin the guarantee
/// the API makes instead: lowestRate &lt;= highestRate.
/// </summary>
public class ValuationRateTests
{
    private const string Chaos = "Metadata/Items/Currency/CurrencyRerollRare";
    private const string Divine = "Metadata/Items/Currency/CurrencyModValues";

    // Copied from a live Standard snapshot of the chaos|divine market.
    private static readonly Dictionary<string, double> LowestRatio = new() { [Divine] = 1, [Chaos] = 610 };
    private static readonly Dictionary<string, double> HighestRatio = new() { [Divine] = 1, [Chaos] = 725 };

    [Fact]
    public void ReferencePerItem_ExpressesReferenceUnitsPerItem()
    {
        Assert.Equal(1.0 / 610, ValuationRate.ReferencePerItem(LowestRatio, Chaos, Divine));
        Assert.Equal(610.0, ValuationRate.ReferencePerItem(LowestRatio, Divine, Chaos));
    }

    [Fact]
    public void ReferencePerItemRange_OrdersAscending_WhenPairOrientationInvertsTheLabels()
    {
        // 1 divine trades for 610..725 chaos, so 1 chaos is worth 1/725..1/610 divine - the
        // published "lowest" label is the numerically higher value in these units.
        var (lowest, highest) = ValuationRate.ReferencePerItemRange(LowestRatio, HighestRatio, Chaos, Divine);

        Assert.Equal(1.0 / 725, lowest);
        Assert.Equal(1.0 / 610, highest);
        Assert.True(lowest <= highest);
    }

    [Fact]
    public void ReferencePerItemRange_KeepsAscendingOrder_WhenItemIsThePricedSide()
    {
        var (lowest, highest) = ValuationRate.ReferencePerItemRange(LowestRatio, HighestRatio, Divine, Chaos);

        Assert.Equal(610.0, lowest);
        Assert.Equal(725.0, highest);
    }

    [Fact]
    public void ReferencePerItemRange_PreservesNull_WhenEitherExtremeIsUnavailable()
    {
        var partial = new Dictionary<string, double> { [Divine] = 1 };

        var (lowest, highest) = ValuationRate.ReferencePerItemRange(LowestRatio, partial, Chaos, Divine);

        Assert.Equal(1.0 / 610, lowest);
        Assert.Null(highest);
    }

    [Fact]
    public void ReferencePerItem_ReturnsNull_WhenItemAmountIsNotPositive()
    {
        var zeroItem = new Dictionary<string, double> { [Divine] = 1, [Chaos] = 0 };

        Assert.Null(ValuationRate.ReferencePerItem(zeroItem, Chaos, Divine));
        Assert.Null(ValuationRate.ReferencePerItem(LowestRatio, Chaos, "Metadata/Items/Currency/Unknown"));
    }
}

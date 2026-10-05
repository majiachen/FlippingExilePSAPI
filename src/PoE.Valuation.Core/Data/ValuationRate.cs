namespace PoE.Valuation.Core.Data;

/// <summary>
/// Rate math for currency-exchange ratio dictionaries. The PoE currency-exchange API publishes,
/// per market pair, ratio dictionaries keyed by item id where each entry is the amount of that
/// item in the observed trades (e.g. <c>lowest_ratio = { "a": 1, "b": 65 }</c> means the lowest
/// observed trade was 1 a for 65 b). The value of the item in reference units is therefore
/// <c>ratio[reference] / ratio[item]</c>, independent of which side of the pair the item is on.
/// </summary>
public static class ValuationRate
{
    /// <summary>
    /// Computes units of <paramref name="reference"/> per 1 unit of <paramref name="itemId"/>
    /// from one ratio dictionary, or null when either entry is missing or the item amount is not
    /// positive (division by zero is never returned).
    /// </summary>
    public static double? ReferencePerItem(IDictionary<string, double> ratio, string itemId, string reference)
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(reference))
            return null;
        if (!ratio.TryGetValue(itemId, out var itemAmount) || itemAmount <= 0)
            return null;
        if (!ratio.TryGetValue(reference, out var referenceAmount))
            return null;

        return referenceAmount / itemAmount;
    }

    /// <summary>
    /// The item's value range in reference units for one snapshot, taken from the pair's two
    /// ratio dictionaries and <em>ordered ascending</em>.
    /// <para>
    /// The published <c>lowest_ratio</c>/<c>highest_ratio</c> labels describe the pair in whichever
    /// orientation the league's data happened to produce — <c>{ divine: 1, chaos: 610 }</c> and
    /// <c>{ divine: 1, chaos: 725 }</c> — so converting to reference-per-item units can flip them:
    /// 1 chaos is worth between 1/725 and 1/610 divine, not the other way round. Ordering the two
    /// extremes here is what makes <c>lowestRate &lt;= highestRate</c> a guarantee of the API rather
    /// than an accident of the market pair's orientation.
    /// </para>
    /// When either extreme is unavailable the pair is returned unsorted, with the null preserved.
    /// </summary>
    public static (double? Lowest, double? Highest) ReferencePerItemRange(
        IDictionary<string, double> lowestRatio,
        IDictionary<string, double> highestRatio,
        string itemId,
        string reference)
    {
        var fromLowestLabel = ReferencePerItem(lowestRatio, itemId, reference);
        var fromHighestLabel = ReferencePerItem(highestRatio, itemId, reference);

        if (fromLowestLabel is null || fromHighestLabel is null)
            return (fromLowestLabel, fromHighestLabel);

        return fromLowestLabel <= fromHighestLabel
            ? (fromLowestLabel, fromHighestLabel)
            : (fromHighestLabel, fromLowestLabel);
    }
}

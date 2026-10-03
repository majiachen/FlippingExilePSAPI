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
}

namespace PoE.Valuation.Core.Data;

/// <summary>
/// The currency metadata paths the system tracks. A currency-exchange market pair from an hourly
/// digest is only persisted when at least one of its two items is one of these paths, so
/// <c>exchange_rate_snapshots</c> holds the Chaos Orb / Divine Orb reference markets rather than
/// every pair the digest publishes.
/// </summary>
public static class TrackedCurrencyMetadata
{
    /// <summary>Chaos Orb — <c>Metadata/Items/Currency/CurrencyRerollRare</c>.</summary>
    public const string CurrencyRerollRare = "Metadata/Items/Currency/CurrencyRerollRare";

    /// <summary>Divine Orb — <c>Metadata/Items/Currency/CurrencyModValues</c>.</summary>
    public const string CurrencyModValues = "Metadata/Items/Currency/CurrencyModValues";

    /// <summary>The accepted metadata paths; a pair is stored when either side is one of these.</summary>
    private static readonly string[] Tracked = { CurrencyRerollRare, CurrencyModValues };

    /// <summary>
    /// True when either side of a market pair is a tracked currency, i.e. the pair should be stored.
    /// Comparison is exact (ordinal) on the full metadata path, matching how the digest publishes
    /// <c>market_pair</c> items and how <c>exchange_rate_snapshots.currency_a</c>/<c>currency_b</c>
    /// store them.
    /// </summary>
    public static bool IsTrackedPair(string currencyA, string currencyB) =>
        IsTracked(currencyA) || IsTracked(currencyB);

    private static bool IsTracked(string currency)
    {
        foreach (var tracked in Tracked)
        {
            if (string.Equals(currency, tracked, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

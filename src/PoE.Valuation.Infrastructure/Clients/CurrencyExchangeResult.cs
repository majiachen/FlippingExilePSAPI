namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>Outcome of a currency-exchange digest fetch.</summary>
public abstract record CurrencyExchangeResult
{
    /// <summary>The requested hour has no published digest yet (API returned 404).</summary>
    public static readonly CurrencyExchangeResult NotReady = new NotReadyResult();

    /// <summary>A full hourly digest was fetched successfully.</summary>
    public sealed record Ok(long NextChangeId, CurrencyExchangeResponse Digest) : CurrencyExchangeResult;

    private sealed record NotReadyResult : CurrencyExchangeResult;
}

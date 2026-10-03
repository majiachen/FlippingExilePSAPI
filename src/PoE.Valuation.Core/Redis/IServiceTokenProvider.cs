namespace PoE.Valuation.Core.Redis;

/// <summary>
/// Cache for the service token used to poll the CX API (tech doc, section 4.2; key
/// <c>service:token:cxapi</c>, stored without a TTL).
/// </summary>
public interface IServiceTokenProvider
{
    /// <summary>Reads the cached service token, or null when none has been stored yet.</summary>
    Task<ServiceTokenEntry?> GetAsync(CancellationToken ct = default);

    /// <summary>Stores (or refreshes) the service token. No TTL: it is cached indefinitely and simply replaced on refresh.</summary>
    Task SetAsync(ServiceTokenEntry entry, CancellationToken ct = default);
}
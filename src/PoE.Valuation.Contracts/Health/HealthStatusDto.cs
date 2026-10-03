namespace PoE.Valuation.Contracts.Health;

/// <summary>Response payload for the /health endpoint.</summary>
public sealed class HealthStatusDto
{
    /// <summary>Overall status of the service ("Healthy" in Phase 1).</summary>
    public string Status { get; init; } = "Healthy";

    /// <summary>UTC time at which the health snapshot was taken.</summary>
    public DateTimeOffset TimestampUtc { get; init; }
}

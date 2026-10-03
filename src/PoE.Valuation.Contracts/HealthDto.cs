namespace PoE.Valuation.Contracts;

/// <summary>Status of a single dependency checked by the health endpoint.</summary>
public sealed record DependencyStatus(string Name, string State);

/// <summary>Payload returned by GET /health.</summary>
public sealed record HealthDto(
    string Status,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<DependencyStatus> Dependencies)
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
}
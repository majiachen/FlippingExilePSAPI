namespace PoE.Valuation.Core.Configuration;

/// <summary>
/// Default rate-limit settings used by the PoE API polling logic.
/// Bound from the "RateLimits" configuration section.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary>Default maximum number of requests allowed per window (PoE default: 30).</summary>
    public int DefaultMaxRequests { get; init; } = 30;

    /// <summary>Default rate-limit window in seconds (PoE default: 300).</summary>
    public int DefaultWindowSeconds { get; init; } = 300;
}
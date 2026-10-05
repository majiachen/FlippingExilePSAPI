namespace PoE.Valuation.Core.Configuration;

/// <summary>
/// Settings for the currency-exchange polling background service.
/// Bound from the "Polling" configuration section.
/// </summary>
public sealed class PollingOptions
{
    public const string SectionName = "Polling";

    /// <summary>Whether the poller runs at all (disable in tests or single-instance deployments).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Delay between polling ticks, in seconds.</summary>
    public int IntervalSeconds { get; init; } = 60;

    /// <summary>Maximum number of hourly digests to fetch within a single catch-up tick.</summary>
    public int MaxCatchUpHoursPerTick { get; init; } = 24;

    /// <summary>
    /// Optional backfill start: the first hourly digest id (UTC) to request on first run.
    /// When null, the poller seeds from the last completed hour instead of walking history.
    /// </summary>
    public DateTimeOffset? InitialChangeIdUtc { get; init; }

    /// <summary>
    /// Whether the item catalogue refresher runs. The catalogue is what maps a stash item name to a
    /// metadata path, so the lookup endpoints return nothing while it is disabled and the table is empty.
    /// </summary>
    public bool ItemCatalogEnabled { get; init; } = true;

    /// <summary>How often the refresher checks the catalogue, in seconds.</summary>
    public int ItemCatalogIntervalSeconds { get; init; } = 3600;

    /// <summary>
    /// A catalogue older than this is refreshed on the next tick. The catalogue only changes with a
    /// game version, so a day is ample.
    /// </summary>
    public int ItemCatalogMaxAgeSeconds { get; init; } = 86_400;
}


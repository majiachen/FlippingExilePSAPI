namespace PoE.Valuation.Core.Polling;

/// <summary>
/// Pure cursor math for the currency-exchange polling loop. The stored cursor is the most recent
/// <c>next_change_id</c> returned by the API: the Unix timestamp (seconds) of the start of the next
/// hourly digest to request.
/// </summary>
public static class CurrencyExchangeCursor
{
    /// <summary>Seconds in one hourly digest window.</summary>
    public const int HourSeconds = 3600;

    /// <summary>
    /// Unix timestamp (seconds) of the start of the last fully completed hour before <paramref name="now"/>.
    /// The API only serves digests for completed hours; requesting the in-progress hour returns 404.
    /// </summary>
    public static long LastCompletedHourStart(DateTimeOffset now) =>
        (now.ToUnixTimeSeconds() / HourSeconds) * HourSeconds - HourSeconds;

    /// <summary>True when there is nothing left to fetch: the next digest id is beyond the last completed hour.</summary>
    public static bool IsCaughtUp(long cursor, DateTimeOffset now) =>
        cursor > LastCompletedHourStart(now);
}

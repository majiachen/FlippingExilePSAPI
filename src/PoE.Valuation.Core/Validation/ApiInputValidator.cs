namespace PoE.Valuation.Core.Validation;

/// <summary>
/// Input validation for the public /api and /auth endpoints. Validation runs before any external
/// call (Redis, SQL, PoE API) so malformed input always fails fast with a 400, independent of
/// service availability.
/// </summary>
public static class ApiInputValidator
{
    /// <summary>
    /// Maximum length of a PoE item id. Stash item ids are long paths (base id plus modifier
    /// suffixes); the currency/market ids used for valuation are shorter, but one bound covers
    /// both.
    /// </summary>
    public const int MaxItemIdLength = 500;

    /// <summary>Maximum length of a league name (e.g. <c>Standard</c>, <c>CSS9Standard</c>).</summary>
    public const int MaxLeagueNameLength = 64;

    /// <summary>
    /// Validates a PoE item id: non-blank, within <see cref="MaxItemIdLength"/>, no control
    /// characters, no whitespace and no ':' — the colon is rejected so the value can also be used
    /// as a flat Redis key part (tech doc, section 3).
    /// </summary>
    public static bool TryValidateItemId(string? itemId, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            error = "Item id is required.";
            return false;
        }
        if (itemId.Length > MaxItemIdLength)
        {
            error = $"Item id must be at most {MaxItemIdLength} characters.";
            return false;
        }
        if (itemId.Any(c => char.IsControl(c) || c == ' ' || c == ':'))
        {
            error = "Item id contains invalid characters (no whitespace, control characters or ':').";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates a league name. PoE league ids are the league name itself and include spaces,
    /// hyphens and parentheses — <c>Standard</c>, <c>CSS9Standard</c>, <c>Solo Self-Found</c>,
    /// <c>Hardcore SSF</c>, <c>Limey Whelps (PL86569)</c>. Allowed: letters, digits and
    /// <c>space - _ . ( ) '</c>, 1..<see cref="MaxLeagueNameLength"/> characters. Control characters
    /// and <c>:</c> are rejected so the value can also be used as a flat Redis key part
    /// (tech doc, section 3).
    /// </summary>
    public static bool TryValidateLeagueName(string? league, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(league) || league.Length > MaxLeagueNameLength)
        {
            error = $"League name is required and must be at most {MaxLeagueNameLength} characters (e.g. 'Standard').";
            return false;
        }
        if (league.Any(c => !IsAllowedLeagueCharacter(c)))
        {
            error = "League name may only contain letters, digits and space - _ . ( ) ' (e.g. 'Standard', 'Solo Self-Found').";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates a client-supplied item name (a stash item's <c>name</c>/<c>base_type</c>) used by the
    /// catalogue lookup. Punctuation is allowed because <see cref="ItemNameNormalizer.Normalize"/>
    /// discards it before anything is compared.
    /// </summary>
    public static bool TryValidateItemName(string? name, out string? error) =>
        ItemNameNormalizer.TryValidateItemName(name, out error);

    private static bool IsAllowedLeagueCharacter(char c) =>
        char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' or '.' or '(' or ')' or '\'';


    /// <summary>
    /// Parses a history range: <c>24h</c>, <c>7d</c> or <c>30d</c> (case-insensitive).
    /// </summary>
    /// <param name="window">The look-back window.</param>
    /// <param name="maxPoints">A safe cap on hourly points in the window (hourly cadence + one margin hour).</param>
    public static bool TryParseHistoryRange(string? range, out TimeSpan window, out int maxPoints, out string? error)
    {
        window = default;
        maxPoints = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(range))
        {
            error = "History range is required (24h, 7d or 30d).";
            return false;
        }

        switch (range.Trim().ToLowerInvariant())
        {
            case "24h":
                window = TimeSpan.FromHours(24);
                maxPoints = 48;
                break;
            case "7d":
                window = TimeSpan.FromDays(7);
                maxPoints = 168;
                break;
            case "30d":
                window = TimeSpan.FromDays(30);
                maxPoints = 720;
                break;
            default:
                error = "History range must be one of: 24h, 7d, 30d.";
                return false;
        }

        return true;
    }
}

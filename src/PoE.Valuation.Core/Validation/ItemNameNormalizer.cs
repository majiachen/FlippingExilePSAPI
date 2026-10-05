using System.Text;

namespace PoE.Valuation.Core.Validation;

/// <summary>
/// Canonical form of an item name used as the catalogue lookup key, and validation for the
/// <c>name</c>/<c>baseType</c> a client supplies.
/// <para>
/// Both sides of the lookup go through <see cref="Normalize"/> — the catalogue entry name and the
/// client query — so the comparison never depends on the caller reproducing PoE's exact spelling.
/// Apostrophes and periods are <em>removed</em> (so <c>Jeweller's Orb</c> and <c>Jewellers Orb</c>
/// are the same item) while every other non-alphanumeric run collapses to a single space.
/// </para>
/// </summary>
public static class ItemNameNormalizer
{
    /// <summary>Upper bound on a client-supplied item name; PoE item names are far shorter.</summary>
    public const int MaxItemNameLength = 120;

    /// <summary>
    /// Validates a client-supplied item name: non-blank, at most <see cref="MaxItemNameLength"/>
    /// characters, no control characters and no <c>:</c> (the colon is rejected so the value can also
    /// appear in a flat Redis key part, tech doc section 3). Punctuation is allowed because
    /// <see cref="Normalize"/> discards it.
    /// </summary>
    public static bool TryValidateItemName(string? name, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Item name is required (e.g. 'Chaos Orb').";
            return false;
        }
        if (name.Length > MaxItemNameLength)
        {
            error = $"Item name must be at most {MaxItemNameLength} characters.";
            return false;
        }
        if (name.Any(c => char.IsControl(c) || c == ':'))
        {
            error = "Item name contains invalid characters (no control characters or ':').";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Lower-cases the name, drops apostrophes and periods, collapses every other run of
    /// non-alphanumeric characters into one space, and trims. Returns an empty string for a
    /// name that contains no letter or digit at all.
    /// </summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var builder = new StringBuilder(name.Length);
        var atWordBoundary = true; // true at the start so leading separators are dropped

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                atWordBoundary = false;
                continue;
            }

            // Removed inside a word: "Jeweller's" -> "jewellers", not "jeweller s".
            if (ch == '\'' || ch == '’' || ch == '.')
                continue;

            if (!atWordBoundary)
            {
                builder.Append(' ');
                atWordBoundary = true;
            }
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
            builder.Length--;

        return builder.ToString();
    }
}

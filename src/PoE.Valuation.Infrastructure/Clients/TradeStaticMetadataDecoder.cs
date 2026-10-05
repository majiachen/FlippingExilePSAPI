using System.Text;
using System.Text.RegularExpressions;

namespace PoE.Valuation.Infrastructure.Clients;

/// <summary>
/// Extracts a PoE metadata path from a trade catalogue entry's <c>image</c> URL.
/// <para>
/// The catalogue publishes no metadata field, but its icon URL embeds one: the segment before the
/// icon hash is a base64url-encoded JSON array describing the icon, and its <c>f</c> member is the
/// game asset path —
/// <c>/gen/image/WzI1LDE0LHsiZiI6IjJESXRlbXMvQ3VycmVuY3kvQ3VycmVuY3lSZXJvbGxSYXJlIiwic2NhbGUiOjF9XQ/…/CurrencyRerollRare.png</c>
/// decodes to <c>[25,14,{"f":"2DItems/Currency/CurrencyRerollRare","scale":1}]</c>.
/// The asset root <c>2DItems</c> is the same tree as the metadata root <c>Metadata/Items</c>, so the
/// decoded value is exactly the id <c>exchange_rate_snapshots.currency_a</c>/<c>currency_b</c> stores.
/// </para>
/// </summary>
public static partial class TradeStaticMetadataDecoder
{
    /// <summary>Asset root used by icon paths; equivalent to the metadata root below.</summary>
    private const string AssetRootPrefix = "2DItems/";

    /// <summary>Metadata root the snapshots are keyed by.</summary>
    private const string MetadataRootPrefix = "Metadata/Items/";

    /// <summary>Minimum length of a plausible base64url payload segment.</summary>
    private const int MinPayloadSegmentLength = 8;

    [GeneratedRegex("\"f\":\"([^\"]+)\"")]
    private static partial Regex FileMemberRegex();

    /// <summary>
    /// Returns true and the metadata path when <paramref name="image"/> contains a decodable icon
    /// payload with a usable <c>f</c> member; false for missing icons, non-base64url segments and
    /// payloads whose file is outside the item asset tree.
    /// </summary>
    public static bool TryDecodeMetadata(string? image, out string metadata)
    {
        metadata = string.Empty;
        if (string.IsNullOrWhiteSpace(image))
            return false;

        // The payload is the segment before the icon hash, but the safest test is "which segment
        // actually decodes to a JSON object with an f member" — the hash segment decodes to bytes
        // that contain no such member, so it is skipped.
        foreach (var segment in image.Split('/'))
        {
            if (segment.Length < MinPayloadSegmentLength)
                continue;

            if (!TryDecodeBase64Url(segment, out var payload))
                continue;

            var match = FileMemberRegex().Match(payload);
            if (!match.Success)
                continue;

            return TryMapAssetPathToMetadata(match.Groups[1].Value, out metadata);
        }

        return false;
    }

    /// <summary>Maps a decoded asset path (<c>2DItems/…</c>) onto its metadata path.</summary>
    private static bool TryMapAssetPathToMetadata(string assetPath, out string metadata)
    {
        metadata = string.Empty;
        var path = assetPath.Replace('\\', '/').Trim();

        if (path.StartsWith(AssetRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var tail = path.Substring(AssetRootPrefix.Length);
            if (tail.Length == 0)
                return false;

            metadata = MetadataRootPrefix + tail;
            return true;
        }

        // Some catalogue revisions already publish the long form; accept it unchanged.
        if (path.StartsWith("Metadata/", StringComparison.OrdinalIgnoreCase))
        {
            metadata = path;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Decodes a base64url segment (RFC 4648 §5 alphabet, padding optional) as UTF-8 text. A segment
    /// that is not valid base64url, or that does not decode to text containing an <c>f</c> member,
    /// is rejected.
    /// </summary>
    private static bool TryDecodeBase64Url(string segment, out string payload)
    {
        payload = string.Empty;

        var base64 = segment.Replace('-', '+').Replace('_', '/');
        var remainder = base64.Length % 4;
        if (remainder == 1)
            return false; // never a valid base64 length
        if (remainder > 0)
            base64 += new string('=', 4 - remainder);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(bytes);
        if (!FileMemberRegex().IsMatch(text))
            return false;

        payload = text;
        return true;
    }
}

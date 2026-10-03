using System.Text.RegularExpressions;

namespace PoE.Valuation.Core.Logging;

/// <summary>Redacts sensitive values from log messages before they reach any provider.</summary>
public static class LogSanitizer
{
    public const string RedactionToken = "[REDACTED]";

    // Well-known OAuth/session parameter names that must never be logged with their value.
    private static readonly Regex SensitiveParameterPattern = new(
        @"(?<key>(?:client_secret|access_token|refresh_token|session_id|state|code_verifier|code_challenge))(?<sep>[=:])\s*(?<value>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Authorization headers / bearer tokens, e.g. "Authorization: Bearer eyJ...".
    private static readonly Regex BearerTokenPattern = new(
        @"(?<scheme>bearer)\s+(?<token>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Replaces every occurrence of the given sensitive values and well-known secret-bearing
    /// parameters in <paramref name="message"/> with <see cref="RedactionToken"/>.
    /// Returns the original instance unchanged when nothing needs redacting (fast path).
    /// </summary>
    public static string Sanitize(string? message, IReadOnlyCollection<string>? sensitiveValues = null)
    {
        if (string.IsNullOrEmpty(message))
            return message ?? string.Empty;

        var hasRegisteredValues = sensitiveValues is not null && sensitiveValues.Count > 0;

        // Fast path: nothing registered and no well-known secret material — skip the
        // replacement work for the common clean-message case.
        if (!hasRegisteredValues && !SensitiveParameterPattern.IsMatch(message) && !BearerTokenPattern.IsMatch(message))
            return message;

        var result = SensitiveParameterPattern.Replace(
            message,
            static m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{RedactionToken}");

        result = BearerTokenPattern.Replace(
            result,
            static m => $"{m.Groups["scheme"].Value} {RedactionToken}");

        if (hasRegisteredValues)
        {
            foreach (var value in sensitiveValues!)
            {
                if (!string.IsNullOrEmpty(value))
                    result = result.Replace(value, RedactionToken, StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
}

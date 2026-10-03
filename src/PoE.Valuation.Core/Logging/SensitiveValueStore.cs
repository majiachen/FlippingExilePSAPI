using System.Collections.Concurrent;

namespace PoE.Valuation.Core.Logging;

/// <summary>
/// Thread-safe registry of values that must never appear in log output
/// (OAuth secrets, session identifiers, PKCE state/verifier values).
/// </summary>
public sealed class SensitiveValueStore
{
    private readonly ConcurrentDictionary<string, byte> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a value for redaction. Null/empty values are ignored.</summary>
    public void Add(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _values[value] = 0;
    }

    /// <summary>Stops redacting the given value. Returns true when it was registered.</summary>
    public bool Remove(string? value) =>
        !string.IsNullOrEmpty(value) && _values.TryRemove(value, out _);

    /// <summary>Returns true when the value is currently registered for redaction (case-insensitive).</summary>
    public bool Contains(string? value) =>
        !string.IsNullOrEmpty(value) && _values.ContainsKey(value!);

    /// <summary>Number of values currently registered for redaction.</summary>
    public int Count => _values.Count;

    /// <summary>Snapshot of all currently registered values.</summary>
    public IReadOnlyCollection<string> All => new List<string>(_values.Keys);
}
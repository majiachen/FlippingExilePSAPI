using Microsoft.Extensions.Logging;

namespace PoE.Valuation.Core.Logging;

/// <summary>ILogger that redacts registered sensitive values from messages and scopes before delegating to a wrapped logger.</summary>
public sealed class SanitizingLogger : ILogger
{
    private readonly ILogger _inner;
    private readonly SensitiveValueStore _store;

    public SanitizingLogger(ILogger inner, SensitiveValueStore store)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var original = state.ToString();
        var sanitized = LogSanitizer.Sanitize(original, _store.Count > 0 ? _store.All : null);

        // Fast path: nothing redacted — keep the original scope object.
        if (ReferenceEquals(sanitized, original))
            return _inner.BeginScope(state);

        return _inner.BeginScope(new SanitizingScopeState(sanitized));
    }

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _inner.Log(
            logLevel,
            eventId,
            LogSanitizer.Sanitize(message, _store.Count > 0 ? _store.All : null),
            exception,
            static (string s, Exception? _) => s);
    }

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    /// <summary>Scope state whose rendered text has already been sanitized.</summary>
    private sealed class SanitizingScopeState
    {
        private readonly string _text;

        public SanitizingScopeState(string text) => _text = text;

        public override string ToString() => _text;
    }
}

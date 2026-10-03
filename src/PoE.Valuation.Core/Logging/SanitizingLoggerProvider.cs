using Microsoft.Extensions.Logging;

namespace PoE.Valuation.Core.Logging;

/// <summary>ILoggerProvider that wraps every logger it creates in a <see cref="SanitizingLogger"/>.</summary>
public sealed class SanitizingLoggerProvider : ILoggerProvider
{
    private readonly ILoggerProvider _inner;
    private readonly SensitiveValueStore _store;
    private readonly List<SanitizingLogger> _loggers = new();

    public SanitizingLoggerProvider(ILoggerProvider inner, SensitiveValueStore store)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new SanitizingLogger(_inner.CreateLogger(categoryName), _store);

        lock (_loggers)
        {
            _loggers.Add(logger);
        }

        return logger;
    }

    public void Dispose()
    {
        List<SanitizingLogger> loggers;

        lock (_loggers)
        {
            loggers = new List<SanitizingLogger>(_loggers);
            _loggers.Clear();
        }

        foreach (var logger in loggers)
            logger.Dispose();

        _inner.Dispose();
    }
}

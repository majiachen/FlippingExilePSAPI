using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Logging;

namespace PoE.Valuation.UnitTests;

public class LogSanitizerTests
{
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = new();
        public List<string> Scopes { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;

            public CapturingLogger(CapturingLoggerProvider provider) => _provider = provider;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                _provider.Scopes.Add(state.ToString());
                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _provider.Messages.Add(formatter(state, exception));

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public void SanitizingLoggerProvider_RedactsRegisteredValues()
    {
        var inner = new CapturingLoggerProvider();
        var store = new SensitiveValueStore();
        store.Add("super-secret-value");

        using var provider = new SanitizingLoggerProvider(inner, store);
        var logger = provider.CreateLogger("Test");

        logger.LogInformation("Token is {Value}", "super-secret-value");
        logger.LogInformation("Plain message without secrets");

        Assert.Contains("[REDACTED]", inner.Messages[0]);
        Assert.DoesNotContain("super-secret-value", string.Join("\n", inner.Messages));
        Assert.Contains("Plain message without secrets", inner.Messages);
    }

    [Fact]
    public void SanitizingLoggerProvider_RedactsScopes()
    {
        var inner = new CapturingLoggerProvider();
        var store = new SensitiveValueStore();
        store.Add("session-abc123");

        using var provider = new SanitizingLoggerProvider(inner, store);
        var logger = provider.CreateLogger("Test");

        using (logger.BeginScope(new { SessionId = "session-abc123" }))
        {
            logger.LogInformation("Scoped message");
        }

        Assert.DoesNotContain("session-abc123", string.Join("\n", inner.Scopes));
        Assert.Contains("[REDACTED]", string.Join("\n", inner.Scopes));
    }

    [Fact]
    public void Sanitize_RedactsBearerTokens()
    {
        const string message = "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload.signature";

        var sanitized = LogSanitizer.Sanitize(message);

        Assert.Contains("Bearer [REDACTED]", sanitized);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", sanitized);
    }

    [Fact]
    public void Sanitize_RedactsWellKnownParameters()
    {
        const string message = "oauth callback with client_secret=abc123 and access_token: xyz789";

        var sanitized = LogSanitizer.Sanitize(message);

        Assert.Contains("client_secret=[REDACTED]", sanitized);
        Assert.Contains("access_token:[REDACTED]", sanitized);
        Assert.DoesNotContain("abc123", sanitized);
        Assert.DoesNotContain("xyz789", sanitized);
    }

    [Fact]
    public void Sanitize_ReturnsSameInstanceForCleanMessages()
    {
        const string message = "Nothing sensitive here";

        var sanitized = LogSanitizer.Sanitize(message);

        Assert.Same(message, sanitized);
    }

    [Fact]
    public void SensitiveValueStore_IgnoresEmptyValues()
    {
        var store = new SensitiveValueStore();

        store.Add(null);
        store.Add("");

        Assert.False(store.Contains(""));
        Assert.False(store.Contains(null));
        Assert.Equal(0, store.Count);
    }
}

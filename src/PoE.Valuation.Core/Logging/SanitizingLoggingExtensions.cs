using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace PoE.Valuation.Core.Logging;

/// <summary>DI wiring for the sanitizing logging pipeline.</summary>
public static class SanitizingLoggingExtensions
{
    /// <summary>
    /// Registers a single sanitized console logger provider as the only logging sink, backed by
    /// the shared <see cref="SensitiveValueStore"/>. Call this after
    /// <c>builder.Logging.ClearProviders()</c> so no unsanitized provider (e.g. the default
    /// Console/Debug providers) can leak secrets to a raw sink.
    /// </summary>
    public static IServiceCollection AddSanitizingLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Single shared store of values that must never reach any sink.
        services.AddSingleton<SensitiveValueStore>();

        // The only registered sink: a sanitized wrapper around the console logger.
        // Registered as an ILoggerProvider singleton so the host's LoggerFactory picks it up.
        services.AddSingleton<ILoggerProvider>(sp => new SanitizingLoggerProvider(
            ActivatorUtilities.CreateInstance<ConsoleLoggerProvider>(sp),
            sp.GetRequiredService<SensitiveValueStore>()));

        return services;
    }
}

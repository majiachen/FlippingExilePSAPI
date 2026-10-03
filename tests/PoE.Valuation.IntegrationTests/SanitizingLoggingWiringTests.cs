using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoE.Valuation.Core.Logging;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Verifies the logging pipeline wiring in the real host: exactly one sanitized console
/// provider is registered, so no unsanitized sink can leak secrets.
/// </summary>
public class SanitizingLoggingWiringTests
{
    [Fact]
    public void OnlySanitizedProviderIsRegistered()
    {
        using var factory = ValuationApiFactory.Create();

        var providers = factory.Services.GetServices<ILoggerProvider>().ToList();

        Assert.NotEmpty(providers);
        Assert.True(
            providers.All(provider => provider is SanitizingLoggerProvider),
            $"Expected only sanitized providers, found: {string.Join(", ", providers.Select(provider => provider.GetType().Name))}");
    }

    [Fact]
    public void SensitiveValueStoreIsRegisteredAsSingleton()
    {
        using var factory = ValuationApiFactory.Create();

        var first = factory.Services.GetRequiredService<SensitiveValueStore>();
        var second = factory.Services.GetRequiredService<SensitiveValueStore>();

        Assert.Same(first, second);
    }
}

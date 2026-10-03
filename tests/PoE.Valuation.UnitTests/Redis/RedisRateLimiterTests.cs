using Moq;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Redis;

namespace PoE.Valuation.UnitTests.Redis;

public class RedisRateLimiterTests
{
    private readonly Mock<IRedisStore> _store = new();
    private readonly RedisRateLimiter _limiter;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    public RedisRateLimiterTests()
    {
        _limiter = new RedisRateLimiter(_store.Object);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(30)]
    public async Task TryAcquireAsync_ReturnsTrue_WhenWithinBudget(int count)
    {
        _store.Setup(s => s.IncrementAsync(RedisKeys.RateLimit("sess-1", "/api/stash"), Window, It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);

        var allowed = await _limiter.TryAcquireAsync("sess-1", "/api/stash", maxRequests: 30, Window);

        Assert.True(allowed);
    }

    [Fact]
    public async Task TryAcquireAsync_ReturnsFalse_WhenBudgetExhausted()
    {
        _store.Setup(s => s.IncrementAsync(RedisKeys.RateLimit("sess-1", "/api/stash"), Window, It.IsAny<CancellationToken>()))
            .ReturnsAsync(31);

        var allowed = await _limiter.TryAcquireAsync("sess-1", "/api/stash", maxRequests: 30, Window);

        Assert.False(allowed);
    }

    [Fact]
    public async Task TryAcquireAsync_UsesSessionScopedKeyAndWindowTtl()
    {
        _store.Setup(s => s.IncrementAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await _limiter.TryAcquireAsync("sess-9", "/api/history", maxRequests: 5, Window);

        _store.Verify(s => s.IncrementAsync(
            "ratelimit:sess-9:/api/history",
            Window,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task TryAcquireAsync_Throws_WhenMaxRequestsNotPositive(int maxRequests)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _limiter.TryAcquireAsync("sess-1", "/api/stash", maxRequests, Window));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task TryAcquireAsync_Throws_WhenWindowNotPositive(long windowMilliseconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _limiter.TryAcquireAsync("sess-1", "/api/stash", 30, TimeSpan.FromMilliseconds(windowMilliseconds)));
    }
}
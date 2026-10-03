using Microsoft.Extensions.Options;
using Moq;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Redis;

namespace PoE.Valuation.UnitTests.Redis;

public class RedisStashCacheTests
{
    private readonly Mock<IRedisStore> _store = new();
    private readonly RedisStashCache _cache;

    private static StashEntry Entry() =>
        new(Array.Empty<StashItem>(), new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero), 3);

    public RedisStashCacheTests()
    {
        _cache = new RedisStashCache(_store.Object, Options.Create(new RedisOptions()));
    }

    [Fact]
    public async Task GetAsync_ReadsStashKey()
    {
        var entry = Entry();
        _store.Setup(s => s.GetAsync<StashEntry>(RedisKeys.Stash("sess-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var result = await _cache.GetAsync("sess-1");

        Assert.Same(entry, result);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNotCached()
    {
        _store.Setup(s => s.GetAsync<StashEntry>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StashEntry?)null);

        Assert.Null(await _cache.GetAsync("sess-1"));
    }

    [Fact]
    public async Task SetAsync_WritesStashKeyWithStashTtl()
    {
        var entry = Entry();

        await _cache.SetAsync("sess-1", entry);

        _store.Verify(s => s.SetAsync(RedisKeys.Stash("sess-1"), entry, TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_UsesConfiguredStashTtl()
    {
        var options = Options.Create(new RedisOptions { StashTtlMinutes = 45 });
        var cache = new RedisStashCache(_store.Object, options);
        var entry = Entry();

        await cache.SetAsync("sess-1", entry);

        _store.Verify(s => s.SetAsync(RedisKeys.Stash("sess-1"), entry, TimeSpan.FromMinutes(45), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_NullEntry_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _cache.SetAsync("sess-1", null!));
    }
}
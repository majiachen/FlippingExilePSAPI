using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Logging;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Redis;
using StackExchange.Redis;

namespace PoE.Valuation.UnitTests.Redis;

public class RedisSessionStoreTests
{
    private readonly Mock<IRedisStore> _store = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly SensitiveValueStore _sensitiveValues = new();
    private readonly RedisSessionStore _sessionStore;

    private static SessionEntry Entry(string sub = "sub-1") =>
        new("at-1", "rt-1", new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero), sub, "player-1", new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero));

    public RedisSessionStoreTests()
    {
        _store.Setup(s => s.Db).Returns(_db.Object);
        _sessionStore = new RedisSessionStore(_store.Object, Options.Create(new RedisOptions()), _sensitiveValues, NullLogger<RedisSessionStore>.Instance);
    }

    [Fact]
    public async Task GetAsync_ReadsSessionKey()
    {
        var entry = Entry();
        _store.Setup(s => s.GetAsync<SessionEntry>(RedisKeys.Session("sess-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var result = await _sessionStore.GetAsync("sess-1");

        Assert.Same(entry, result);
    }

    [Fact]
    public async Task SetAsync_WritesSessionKeyWithSessionTtl_AndRegistersSessionIdAsSensitive()
    {
        var entry = Entry();

        await _sessionStore.SetAsync("sess-1", entry);

        _store.Verify(s => s.SetAsync(RedisKeys.Session("sess-1"), entry, TimeSpan.FromDays(90), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_sensitiveValues.Contains("sess-1"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAsync_RemovesSessionIdAsSensitiveOnlyWhenKeyExisted(bool existed)
    {
        _store.Setup(s => s.DeleteAsync(RedisKeys.Session("sess-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existed);

        // Simulate an existing session: SetAsync registers the id in the sensitive-value redaction set.
        await _sessionStore.SetAsync("sess-1", Entry());
        Assert.True(_sensitiveValues.Contains("sess-1"));

        var result = await _sessionStore.DeleteAsync("sess-1");

        Assert.Equal(existed, result);

        // A session id may only be dropped from the redaction set when the key was actually deleted.
        Assert.Equal(!existed, _sensitiveValues.Contains("sess-1"));
    }

    [Fact]
    public async Task ReplaceAsync_WritesPendingKeyThenRenamesThenRestoresFullSessionTtl()
    {
        var entry = Entry();
        var order = new List<string>();

        _store.Setup(s => s.SetAsync<SessionEntry>(RedisKeys.SessionPending("sess-1"), It.IsAny<SessionEntry>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, SessionEntry, TimeSpan?, CancellationToken>((key, value, ttl, _) => order.Add($"set-pending:{key}:{ttl}"))
            .Returns(Task.CompletedTask);
        _store.Setup(s => s.RenameAsync(RedisKeys.SessionPending("sess-1"), RedisKeys.Session("sess-1"), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("rename"))
            .ReturnsAsync(true);
        _db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _sessionStore.ReplaceAsync("sess-1", entry);

        Assert.Equal(new[] { $"set-pending:{RedisKeys.SessionPending("sess-1")}:00:05:00", "rename" }, order);
        _db.Verify(d => d.KeyExpireAsync(RedisKey("session:sess-1"), TimeSpan.FromDays(90), ExpireWhen.Always, CommandFlags.None), Times.Once);
        Assert.True(_sensitiveValues.Contains("sess-1"));
    }

    [Fact]
    public async Task ReplaceAsync_Throws_WhenRenameFails()
    {
        _store.Setup(s => s.SetAsync<SessionEntry>(RedisKeys.SessionPending("sess-1"), It.IsAny<SessionEntry>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _store.Setup(s => s.RenameAsync(RedisKeys.SessionPending("sess-1"), RedisKeys.Session("sess-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sessionStore.ReplaceAsync("sess-1", Entry()));
    }

    [Fact]
    public async Task SetAndReplace_NullEntry_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sessionStore.SetAsync("sess-1", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sessionStore.ReplaceAsync("sess-1", null!));
    }

    private static RedisKey RedisKey(string value) => value;
}
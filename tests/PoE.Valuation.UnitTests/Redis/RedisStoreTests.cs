using Moq;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Redis;
using StackExchange.Redis;

namespace PoE.Valuation.UnitTests.Redis;

public class RedisStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _connection = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisStore _store;

    public RedisStoreTests()
    {
        _connection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<CommandFlags>())).Returns(_db.Object);
        _store = new RedisStore(_connection.Object);
    }

    [Fact]
    public void Db_PassesThroughSharedDatabaseHandle()
    {
        Assert.Same(_db.Object, _store.Db);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyMissing()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _store.GetAsync<SessionEntry>("session:missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_DeserializesCamelCaseJson()
    {
        var json = """{"accessToken":"at-1","refreshToken":"rt-1","accessTokenExpiresAt":"2026-09-27T10:00:00Z","sub":"sub-1","username":"player-1","createdAt":"2026-09-21T10:00:00Z"}""";
        _db.Setup(d => d.StringGetAsync(RedisKey("session:1"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(json));

        var result = await _store.GetAsync<SessionEntry>("session:1");

        Assert.NotNull(result);
        Assert.Equal("at-1", result!.AccessToken);
        Assert.Equal("rt-1", result.RefreshToken);
        Assert.Equal("sub-1", result.Sub);
        Assert.Equal("player-1", result.Username);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero), result.CreatedAt);
    }

    [Fact]
    public async Task SetAsync_SerializesCamelCaseJson_OmittingNulls()
    {
        var lastRefreshedAt = new DateTimeOffset(2026, 9, 27, 8, 30, 0, TimeSpan.Zero);
        var entry = new StashEntry(
            new[]
            {
                new StashItem("i-1", "Standard", "Sword", "Sword", 3, 83, null, null, null, Corrupted: true, Verified: false, Note: null)
            },
            lastRefreshedAt,
            LeagueId: "Solo Self-Found");
        var expectedJson =
            """{"items":[{"itemId":"i-1","league":"Standard","name":"Sword","baseType":"Sword","frameType":3,"itemLevel":83,"corrupted":true,"verified":false}],"lastRefreshedAt":"2026-09-27T08:30:00+00:00","leagueId":"Solo Self-Found"}""";

        _db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _store.SetAsync(RedisKeys.Stash("sess-1"), entry, TimeSpan.FromMinutes(15));

        _db.Verify(d => d.StringSetAsync(
                RedisKey("stash:sess-1"),
                It.Is<RedisValue>(v => v.ToString() == expectedJson),
                TimeSpan.FromMinutes(15),
                When.Always,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_NullValue_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SetAsync<string>("session:1", null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAsync_PassesThroughDatabaseResult(bool deleted)
    {
        _db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(deleted);

        var result = await _store.DeleteAsync("session:1");

        Assert.Equal(deleted, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistsAsync_PassesThroughDatabaseResult(bool exists)
    {
        _db.Setup(d => d.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(exists);

        var result = await _store.ExistsAsync("session:1");

        Assert.Equal(exists, result);
    }

    [Fact]
    public async Task SetIfNotExistsAsync_UsesWhenNotExistsAndTtl()
    {
        _db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var created = await _store.SetIfNotExistsAsync("lock:polling:3", "owner-1", TimeSpan.FromMinutes(5));

        Assert.True(created);
        _db.Verify(d => d.StringSetAsync(
                RedisKey("lock:polling:3"),
                It.Is<RedisValue>(v => v.ToString() == "owner-1"),
                TimeSpan.FromMinutes(5),
                When.NotExists,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_SetsTtlOnlyOnFirstIncrement()
    {
        var ttl = TimeSpan.FromSeconds(60);
        var incrementCalls = 0;
        _db.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, long value, CommandFlags flags) => Task.FromResult((long)(++incrementCalls)));
        _db.Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var first = await _store.IncrementAsync("ratelimit:sess-1:/api/stash", ttl);
        var second = await _store.IncrementAsync("ratelimit:sess-1:/api/stash", ttl);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        _db.Verify(d => d.KeyExpireAsync(
                RedisKey("ratelimit:sess-1:/api/stash"),
                ttl,
                ExpireWhen.Always,
                CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task RenameAsync_PassesThroughDatabaseResult()
    {
        _db.Setup(d => d.KeyRenameAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var result = await _store.RenameAsync("session:1:pending", "session:1");

        Assert.True(result);
        _db.Verify(d => d.KeyRenameAsync(
                RedisKey("session:1:pending"),
                RedisKey("session:1"),
                When.Always,
                CommandFlags.None),
            Times.Once);
    }

    private static RedisKey RedisKey(string value) => value;
}
using PoE.Valuation.Core.Redis;

namespace PoE.Valuation.UnitTests.Redis;

public class RedisKeysTests
{
    [Fact]
    public void CxApiServiceTokenKey_IsConstantPath()
    {
        Assert.Equal("service:token:cxapi", RedisKeys.CxApiServiceTokenKey);
    }

    [Fact]
    public void OAuthState_BuildsNamespacedKey()
    {
        Assert.Equal("oauth:state:state-abc", RedisKeys.OAuthState("state-abc"));
    }

    [Fact]
    public void Session_BuildsNamespacedKey()
    {
        Assert.Equal("session:sess-1", RedisKeys.Session("sess-1"));
    }

    [Fact]
    public void SessionPending_IsSessionKeyWithPendingSuffix()
    {
        Assert.Equal("session:sess-1:pending", RedisKeys.SessionPending("sess-1"));
    }

    [Fact]
    public void Stash_BuildsNamespacedKey()
    {
        Assert.Equal("stash:sess-1", RedisKeys.Stash("sess-1"));
    }

    [Fact]
    public void History_BuildsNamespacedKeyWithItemAndRange()
    {
        Assert.Equal("history:item-9:last30", RedisKeys.History("item-9", "last30"));
    }

    [Fact]
    public void PollingLock_BuildsNamespacedKeyWithLeagueId()
    {
        Assert.Equal("lock:polling:3", RedisKeys.PollingLock(3));
    }

    [Fact]
    public void RateLimit_BuildsNamespacedKeyWithSessionAndEndpoint()
    {
        Assert.Equal("ratelimit:sess-1:/api/stash", RedisKeys.RateLimit("sess-1", "/api/stash"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a:b")]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\0b")]
    [InlineData("a\tb")]
    public void ValidatePart_RejectsEmptyOrMalformedParts(string part)
    {
        Assert.Throws<ArgumentException>(() => RedisKeys.Session(part));
        Assert.Throws<ArgumentException>(() => RedisKeys.History("item-9", part));
        Assert.Throws<ArgumentException>(() => RedisKeys.RateLimit(part, "/api/stash"));
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("with space")]
    [InlineData("with/slash")]
    public void ValidatePart_AcceptsNormalParts(string part)
    {
        Assert.Equal($"session:{part}", RedisKeys.Session(part));
    }
}
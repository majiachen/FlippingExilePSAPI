using Microsoft.Extensions.Configuration;
using PoE.Valuation.Core.Configuration;

namespace PoE.Valuation.UnitTests;

public class ConfigurationBindingTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void OAuthOptions_Binds_LegacyKeys()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["OAuth:ClientId"] = "client-123",
            ["OAuth:ClientSecret"] = "secret-456",
            ["OAuth:RedirectUri"] = "https://localhost/oauth/callback"
        });

        var options = config.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions();

        Assert.Equal("client-123", options.ClientId);
        Assert.Equal("secret-456", options.ClientSecret);
        Assert.Equal("https://localhost/oauth/callback", options.RedirectUri);
    }

    [Fact]
    public void RedisOptions_Binds_ConnectionString()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "localhost:6379"
        });

        var options = config.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        Assert.Equal("localhost:6379", options.ConnectionString);
    }

    [Fact]
    public void SqlOptions_Binds_ConnectionString()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Sql:ConnectionString"] = "Host=localhost;Port=5432;Database=poevaluation;Username=postgres;Password=postgres"
        });

        var options = config.GetSection(SqlOptions.SectionName).Get<SqlOptions>() ?? new SqlOptions();

        Assert.Equal("Host=localhost;Port=5432;Database=poevaluation;Username=postgres;Password=postgres", options.ConnectionString);
    }

    [Fact]
    public void PoEApiOptions_Has_SaneDefaults_WhenSectionMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var options = config.GetSection(PoEApiOptions.SectionName).Get<PoEApiOptions>() ?? new PoEApiOptions();

        Assert.Equal("https://api.pathofexile.com", options.ApiBaseUrl);
        Assert.Equal("https://web.poecdn.com/api/currency-exchange", options.CurrencyExchangeBaseUrl);
        Assert.Equal("PoEValuation/1.0", options.UserAgent);
        Assert.Equal(30, options.RequestTimeoutSeconds);
    }

    [Fact]
    public void PoEApiOptions_Binds_AllProperties()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["PathOfExile:ApiBaseUrl"] = "https://api.example.com",
            ["PathOfExile:CurrencyExchangeBaseUrl"] = "https://cdn.example.com/api/currency-exchange",
            ["PathOfExile:UserAgent"] = "TestAgent/2.0",
            ["PathOfExile:RequestTimeoutSeconds"] = "15"
        });

        var options = config.GetSection(PoEApiOptions.SectionName).Get<PoEApiOptions>() ?? new PoEApiOptions();

        Assert.Equal("https://api.example.com", options.ApiBaseUrl);
        Assert.Equal("https://cdn.example.com/api/currency-exchange", options.CurrencyExchangeBaseUrl);
        Assert.Equal("TestAgent/2.0", options.UserAgent);
        Assert.Equal(15, options.RequestTimeoutSeconds);
    }

    [Fact]
    public void PollingOptions_Has_SaneDefaults_WhenSectionMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var options = config.GetSection(PollingOptions.SectionName).Get<PollingOptions>() ?? new PollingOptions();

        Assert.True(options.Enabled);
        Assert.Equal(60, options.IntervalSeconds);
        Assert.Equal(24, options.MaxCatchUpHoursPerTick);
        Assert.Null(options.InitialChangeIdUtc);
    }

    [Fact]
    public void PollingOptions_Binds_AllProperties()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Polling:Enabled"] = "false",
            ["Polling:IntervalSeconds"] = "120",
            ["Polling:MaxCatchUpHoursPerTick"] = "6",
            ["Polling:InitialChangeIdUtc"] = "2026-09-21T18:00:00Z"
        });

        var options = config.GetSection(PollingOptions.SectionName).Get<PollingOptions>() ?? new PollingOptions();

        Assert.False(options.Enabled);
        Assert.Equal(120, options.IntervalSeconds);
        Assert.Equal(6, options.MaxCatchUpHoursPerTick);
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 18, 0, 0, TimeSpan.Zero), options.InitialChangeIdUtc);
    }

    [Fact]
    public void RateLimitOptions_Has_SaneDefaults_WhenSectionMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var options = config.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        Assert.Equal(30, options.DefaultMaxRequests);
        Assert.Equal(300, options.DefaultWindowSeconds);
    }
}

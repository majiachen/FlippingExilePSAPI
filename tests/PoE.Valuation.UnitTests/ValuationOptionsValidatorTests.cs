using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Validation;

namespace PoE.Valuation.UnitTests;

public class ValuationOptionsValidatorTests
{
    private static OAuthOptions OAuth(string? clientId = "client-123", string? clientSecret = "secret-456", string? redirectUri = "https://localhost/oauth/callback") =>
        new() { ClientId = clientId, ClientSecret = clientSecret, RedirectUri = redirectUri };

    private static RedisOptions Redis(string? connectionString = "localhost:6379") =>
        new() { ConnectionString = connectionString };

    private static SqlOptions Sql(string? connectionString = "Host=localhost;Port=5432;Database=PoEValuation;Username=postgres;Password=postgres") =>
        new() { ConnectionString = connectionString };

    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        Assert.Empty(ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(), new PoEApiOptions()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingClientId_ReportsError(string? clientId)
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(clientId: clientId), Redis(), Sql(), new PoEApiOptions());

        Assert.Contains(errors, e => e.Contains("OAuth:ClientId"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_MissingClientSecret_ReportsError(string? clientSecret)
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(clientSecret: clientSecret), Redis(), Sql(), new PoEApiOptions());

        Assert.Contains(errors, e => e.Contains("OAuth:ClientSecret"));
    }

    [Theory]
    [InlineData("http://localhost/oauth/callback")]
    [InlineData("localhost/oauth/callback")]
    public void Validate_NonHttpsRedirectUri_ReportsError(string redirectUri)
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(redirectUri: redirectUri), Redis(), Sql(), new PoEApiOptions());

        Assert.Contains(errors, e => e.Contains("https"));
    }

    [Fact]
    public void Validate_EmptyRedisConnectionString_ReportsError()
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(), Redis(connectionString: ""), Sql(), new PoEApiOptions());

        Assert.Contains(errors, e => e.Contains("Redis:ConnectionString"));
    }

    [Fact]
    public void Validate_EmptySqlConnectionString_ReportsError()
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(connectionString: null), new PoEApiOptions());

        Assert.Contains(errors, e => e.Contains("Sql:ConnectionString"));
    }

    [Fact]
    public void Validate_RelativeApiBaseUrl_ReportsError()
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(), new PoEApiOptions { ApiBaseUrl = "pathofexile.com/api" });

        Assert.Contains(errors, e => e.Contains("PathOfExile:ApiBaseUrl"));
    }

    [Fact]
    public void Validate_ValidCustomBaseUrls_ReturnsNoErrors()
    {
        var poe = new PoEApiOptions
        {
            ApiBaseUrl = "https://api.example.com",
            CurrencyExchangeBaseUrl = "http://cdn.example.com/api/currency-exchange"
        };

        Assert.Empty(ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(), poe));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("pathofexile.com/api")]
    [InlineData("ftp://example.com/api")]
    public void Validate_InvalidApiBaseUrl_ReportsError(string? apiBaseUrl)
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(), new PoEApiOptions { ApiBaseUrl = apiBaseUrl });

        Assert.Contains(errors, e => e.Contains("PathOfExile:ApiBaseUrl"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("web.poecdn.com/api/currency-exchange")]
    [InlineData("ftp://example.com/currency-exchange")]
    public void Validate_InvalidCurrencyExchangeBaseUrl_ReportsError(string? currencyExchangeBaseUrl)
    {
        var errors = ValuationOptionsValidator.Validate(OAuth(), Redis(), Sql(), new PoEApiOptions { CurrencyExchangeBaseUrl = currencyExchangeBaseUrl });

        Assert.Contains(errors, e => e.Contains("PathOfExile:CurrencyExchangeBaseUrl"));
    }
}

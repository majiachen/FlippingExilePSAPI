using System.Net;
using System.Text.Json;

namespace PoE.Valuation.IntegrationTests;

public class HealthEndpointTests : IClassFixture<ValuationApiFactory>
{
    private readonly ValuationApiFactory _factory;

    public HealthEndpointTests(ValuationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_ReturnsOk_WithHealthyStatus()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }
}

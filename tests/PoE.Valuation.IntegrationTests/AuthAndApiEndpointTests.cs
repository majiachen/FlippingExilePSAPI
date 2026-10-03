using System.Net;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Exercises the routing and the security envelope of the auth + /api surface without any backend:
/// the factory runs with no reachable Redis/SQL, so these tests assert the paths that resolve
/// before (or independent of) a backend call — session gating (401), the callback's fail-fast
/// validation (400 before Redis), and logout (204) — proving the routes are wired and degrade
/// gracefully. Happy paths that need live Redis/SQL are covered by unit tests over the fakes.
/// </summary>
public class AuthAndApiEndpointTests : IClassFixture<ValuationApiFactory>
{
    private readonly HttpClient _client;

    public AuthAndApiEndpointTests(ValuationApiFactory factory) =>
        _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/valuation/some-item-id")]
    [InlineData("/api/history/some-item-id?range=24h")]
    [InlineData("/api/stash?league=Standard")]
    public async Task ApiEndpoints_WithoutSession_Return401(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthCallback_WithInvalidState_Returns400_BeforeTouchingRedis()
    {
        // "garbage" is not a well-formed PKCE state, so SessionService rejects it before any Redis
        // access — this returns 400 even though no Redis is available in the test environment.
        using var response = await _client.GetAsync("/auth/callback?code=abc&state=garbage");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthLogout_WithoutSession_Returns204()
    {
        using var response = await _client.PostAsync("/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
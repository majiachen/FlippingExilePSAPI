using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace PoE.Valuation.IntegrationTests;

/// <summary>
/// Unit-level tests of <see cref="PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware"/> that
/// drive it with a throwing delegate against a synthetic <see cref="HttpContext"/> — no host,
/// Redis, SQL or live PoE required. Verifies the /api-scoped mapping of PoE REST failures to 502
/// and that other paths / exception types are left untouched.
/// </summary>
public class PoeUnavailableMiddlewareTests
{
    private static DefaultHttpContext NewContext(string path)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Method = "GET";
        context.Request.Path = new PathString(path);
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
    }

    [Fact]
    public async Task Invoke_ApiPath_WhenPoeFails_Returns502()
    {
        var context = NewContext("/api/stash");
        var middleware = new PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware(
            _ => Task.FromException(new HttpRequestException("PoE is down")),
            NullLogger<PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.Contains("Upstream unavailable", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task Invoke_NonApiPath_WhenPoeFails_Propagates()
    {
        var context = NewContext("/health");
        var middleware = new PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware(
            _ => Task.FromException(new HttpRequestException("PoE is down")),
            NullLogger<PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task Invoke_ApiPath_WhenOtherError_Propagates()
    {
        var context = NewContext("/api/stash");
        var middleware = new PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware(
            _ => Task.FromException(new InvalidOperationException("boom")),
            NullLogger<PoE.Valuation.Api.Middleware.PoeUnavailableMiddleware>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
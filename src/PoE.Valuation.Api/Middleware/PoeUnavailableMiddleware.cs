using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PoE.Valuation.Api.Middleware;

/// <summary>
/// Maps failures of the upstream PoE REST API on the <c>/api/*</c> surface to 502 Bad Gateway
/// (tech doc, section 6): the API is a gateway in front of PoE's endpoints (the league list and
/// the account stash, served by <c>PoeStashClient</c>), so a PoE outage or error response must
/// surface as a retryable bad-gateway rather than a 500.
/// </summary>
/// <remarks>
/// <see cref="HttpRequestException"/> covers both a non-2xx PoE response (thrown by
/// <c>EnsureSuccessStatusCode</c>) and a connection-level failure (DNS refused, timeout surface).
/// It is caught only on <c>/api</c> paths — the client is never used elsewhere in the request
/// pipeline (the poller runs out-of-band and the OAuth flow lives under <c>/auth</c>). A client-side
/// cancellation is left to the host's default handling, since it is not a PoE outage.
/// </remarks>
public sealed class PoeUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PoeUnavailableMiddleware> _logger;

    public PoeUnavailableMiddleware(RequestDelegate next, ILogger<PoeUnavailableMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (HttpRequestException ex) when (!context.Response.HasStarted)
        {
            _logger.LogWarning(ex, "PoE API unavailable for {Method} {Path}; responding 502.",
                context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails { Status = 502, Title = "Upstream unavailable" },
                context.RequestAborted);
        }
    }
}
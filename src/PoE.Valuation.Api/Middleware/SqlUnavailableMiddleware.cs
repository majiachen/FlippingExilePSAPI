using Microsoft.Extensions.Logging;
using Npgsql;

namespace PoE.Valuation.Api;

/// <summary>
/// Maps Postgres failures on the <c>/api/*</c> surface to 503 Service Unavailable (tech doc,
/// section 6) — the API surface of <see cref="RedisUnavailableMiddleware"/>: the data path depends
/// on Postgres, and a database outage must surface as a retryable service-unavailable rather than
/// a 500.
/// </summary>
/// <remarks>
/// <see cref="NpgsqlException"/> covers connection refused, timeout and other driver-level
/// failures; <see cref="OperationCanceledException"/> covers request-scoped cancellation.
/// Everything else propagates (a 500 is the correct signal for an unexpected error). The request
/// body is not consumed before the short-circuit, so the response is safe to write even when the
/// exception was thrown mid-body-read.
/// </remarks>
public sealed class SqlUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SqlUnavailableMiddleware> _logger;

    public SqlUnavailableMiddleware(RequestDelegate next, ILogger<SqlUnavailableMiddleware> logger)
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
        catch (NpgsqlException ex) when (!context.Response.HasStarted)
        {
            _logger.LogWarning(ex, "Postgres unavailable for {Method} {Path}; responding 503.",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(
                """{"type":"https://datatracker.ietf.org/doc/html/rfc9457#section-4.3.1","title":"Service unavailable","status":503,"detail":"The database is temporarily unavailable. Please retry."}""");
        }
        catch (OperationCanceledException) when (!context.Response.HasStarted)
        {
            // Client aborted the request: 499-style status, nothing to log at warning level.
            _logger.LogDebug("Request cancelled for {Method} {Path}; responding 499.",
                context.Request.Method, context.Request.Path);
            // 499 "Client Closed Request" is non-standard and absent from StatusCodes/HttpStatusCode, so use the literal.
            context.Response.StatusCode = 499;
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace PoE.Valuation.Api.Middleware;

/// <summary>
/// Translates <see cref="RedisConnectionException"/> into an HTTP 503 problem response so a
/// temporarily unavailable Redis degrades gracefully instead of bubbling up as a 500
/// (tech doc, sections 6.1 and 9).
/// </summary>
public sealed class RedisUnavailableMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RedisUnavailableMiddleware> _logger;

    public RedisUnavailableMiddleware(RequestDelegate next, ILogger<RedisUnavailableMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable; returning 503 for {Path}", context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails { Status = 503, Title = "Cache unavailable" },
                context.RequestAborted);
        }
    }
}
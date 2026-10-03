using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PoE.Valuation.Infrastructure.Services;

namespace PoE.Valuation.Api.Endpoints;

/// <summary>
/// PKCE OAuth login / callback / logout routes (tech doc, section 5). <see cref="SessionService"/>
/// performs the flow; these handlers only translate HTTP to its calls and set/remove the session
/// cookie. The callback is a browser flow: on success the <c>poe_sid</c> cookie is issued and the
/// client is redirected to the app root.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/auth/login", async (SessionService sessionService) =>
        {
            var authorizeUrl = await sessionService.BeginLoginAsync();
            return Results.Redirect(authorizeUrl.AbsoluteUri);
        });

        endpoints.MapGet("/auth/callback", async (HttpContext ctx, SessionService sessionService, string? code, string? state) =>
        {
            var result = await sessionService.HandleCallbackAsync(code, state);
            if (result is not SessionService.CallbackResult.Success success)
                return Results.Problem(title: "Authentication failed", detail: "The authorization callback was invalid or has expired.", statusCode: 400);

            SessionCookie.Append(ctx.Response, success.SessionId);
            return Results.Redirect("/");
        });

        endpoints.MapPost("/auth/logout", async (HttpContext ctx, SessionService sessionService) =>
        {
            var sessionId = SessionCookie.GetSessionId(ctx.Request);
            if (sessionId is not null)
                await sessionService.LogoutAsync(sessionId);

            SessionCookie.Remove(ctx.Response);
            return Results.NoContent();
        });

        return endpoints;
    }
}
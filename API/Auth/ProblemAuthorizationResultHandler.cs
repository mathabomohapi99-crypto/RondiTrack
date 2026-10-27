using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Telemetry;

namespace RondiTrack.Auth;

// Replaces the default empty 401/403 with the same problem+json shape as the rest of the API
public sealed class ProblemAuthorizationResultHandler(ILogger<ProblemAuthorizationResultHandler> logger)   // NEW 6.2: logger
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)   // no token, or a bad / expired token
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized",
                "A valid access token is required.");
            return;
        }

        if (authorizeResult.Forbidden)   // signed in, but not allowed
        {
            RondiMetrics.AuthorizationRefused(RefusalResults.Forbidden);   // NEW 6.2: count it
            await WriteAsync(context, StatusCodes.Status403Forbidden, "Forbidden",
                "You do not have permission to do this.");
            return;
        }

        await next(context);   // allowed
    }

    private async Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        var correlationId = CorrelationId.For(context);   // CHANGED 6.2: W3C trace id

        // NEW 6.2: 4xx at Information. Only our own user id (or "anonymous"), never a token, email or name.
        logger.LogInformation("Request rejected with status {Status} for user {UserId}. CorrelationId: {CorrelationId}",
            status, context.User.GetUserId()?.ToString() ?? "anonymous", correlationId);

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
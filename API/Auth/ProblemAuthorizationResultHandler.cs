using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Auth;

// Replaces the default empty 401/403 with the same problem+json shape as the rest of the API
public sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
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
            await WriteAsync(context, StatusCodes.Status403Forbidden, "Forbidden",
                "You do not have permission to do this.");
            return;
        }

        await next(context);   // allowed
    }

    private static async Task WriteAsync(HttpContext context, int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["correlationId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Telemetry;
using RondiTrack.Web;
using System.Text.Json;

namespace RondiTrack.Errors;

public sealed class RondiExceptionHandler(ILogger<RondiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // UseExceptionHandler wipes response headers (including CORS), so put them back
        await CorsSetup.ReapplyAsync(httpContext);

        var correlationId = CorrelationId.For(httpContext);

        var status = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var detail = "An unexpected error occurred.";
        Dictionary<string, string[]>? errors = null;   // field -> messages, validation only

        if (exception is ValidationException vex)
        {
            status = StatusCodes.Status400BadRequest;
            title = "Validation Failed";
            detail = "One or more fields are invalid.";
            errors = vex.Errors
                .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }
        else if (exception is DomainNotFoundException) { status = 404; title = "Not Found"; detail = exception.Message; }
        else if (exception is DomainConflictException) { status = 409; title = "Conflict"; detail = exception.Message; }
        else if (exception is DomainReferenceException) { status = 422; title = "Unprocessable Entity"; detail = exception.Message; }
        else if (exception is DomainValidationException) { status = 400; title = "Bad Request"; detail = exception.Message; }
        else if (exception is ApiBadRequestException) { status = 400; title = "Bad Request"; detail = exception.Message; }
        else if (exception is PreconditionRequiredException) { status = 428; title = "Precondition Required"; detail = exception.Message; }
        else if (exception is InvalidCredentialsException or InvalidRefreshTokenException)
        {
            status = 401; title = "Unauthorized"; detail = exception.Message;
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }
        else if (exception is ForbiddenException) { status = 403; title = "Forbidden"; detail = exception.Message; }
        else if (exception is DbUpdateConcurrencyException)
        {
            if (httpContext.Request.Headers.ContainsKey("If-Match"))
            {
                status = 412; title = "Precondition Failed";
                detail = "This record was changed by someone else since you loaded it. Reload it and try again.";
            }
            else
            {
                status = 409; title = "Conflict";
                detail = "This record was changed by someone else at the same time. Reload it and try again.";
            }
        }
        else if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            status = 409; title = "Conflict";
            detail = "That record already exists (it would break a uniqueness rule).";
        }

        if (status >= 500)
            logger.LogError(exception, "Request failed with status {Status}. CorrelationId: {CorrelationId}", status, correlationId);
        else
            logger.LogInformation("Request rejected with status {Status} ({ExceptionType}). CorrelationId: {CorrelationId}",
                status, exception.GetType().Name, correlationId);

        var problemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problemDetails.Extensions["correlationId"] = correlationId;
        if (errors is not null) problemDetails.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), cancellationToken);
        return true;
    }
}

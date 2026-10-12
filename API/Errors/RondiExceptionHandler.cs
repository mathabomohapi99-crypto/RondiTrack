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
using System.Text.Json;

namespace RondiTrack.Errors;

public sealed class RondiExceptionHandler(ILogger<RondiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId.For(httpContext);   // CHANGED 6.2: the W3C trace id, not TraceIdentifier

        var status = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var detail = "An unexpected error occurred.";   // the ONLY text a client sees for a 5xx

        if (exception is ValidationException vex)
        {
            status = StatusCodes.Status400BadRequest;
            title = "Validation Failed";
            detail = string.Join(" ", vex.Errors.Select(e => e.ErrorMessage));
        }
        else if (exception is DomainNotFoundException)
        {
            status = StatusCodes.Status404NotFound;
            title = "Not Found";
            detail = exception.Message;
        }
        else if (exception is DomainConflictException)
        {
            status = StatusCodes.Status409Conflict;
            title = "Conflict";
            detail = exception.Message;
        }
        else if (exception is DomainReferenceException)
        {
            status = StatusCodes.Status422UnprocessableEntity;
            title = "Unprocessable Entity";
            detail = exception.Message;
        }
        else if (exception is DomainValidationException)
        {
            status = StatusCodes.Status400BadRequest;
            title = "Bad Request";
            detail = exception.Message;
        }
        // ADDED 5.3: bad paging / sort / filter / token input -> 400
        else if (exception is ApiBadRequestException)
        {
            status = StatusCodes.Status400BadRequest;
            title = "Bad Request";
            detail = exception.Message;
        }
        // ADDED 5.3: update sent without If-Match -> 428
        else if (exception is PreconditionRequiredException)
        {
            status = StatusCodes.Status428PreconditionRequired;
            title = "Precondition Required";
            detail = exception.Message;
        }
        // NEW 6.1: wrong login or bad refresh token -> 401 (the message is the same for every cause)
        else if (exception is InvalidCredentialsException or InvalidRefreshTokenException)
        {
            status = StatusCodes.Status401Unauthorized;
            title = "Unauthorized";
            detail = exception.Message;
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }
        // NEW 6.1: signed in but missing the role in this stokvel -> 403
        else if (exception is ForbiddenException)
        {
            status = StatusCodes.Status403Forbidden;
            title = "Forbidden";
            detail = exception.Message;
        }
        // ADDED 5.3: xmin token was stale. If the client sent If-Match, the precondition failed (412).
        // If there was no If-Match (a race inside the server, e.g. two payouts at once), it is a plain 409.
        else if (exception is DbUpdateConcurrencyException)
        {
            if (httpContext.Request.Headers.ContainsKey("If-Match"))
            {
                status = StatusCodes.Status412PreconditionFailed;
                title = "Precondition Failed";
                detail = "This record was changed by someone else since you loaded it. Reload it and try again.";
            }
            else
            {
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = "This record was changed by someone else at the same time. Reload it and try again.";
            }
        }
        // ADDED 5.3: a database unique constraint fired (SQLSTATE 23505) -> 409 instead of 500
        else if (exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            status = StatusCodes.Status409Conflict;
            title = "Conflict";
            detail = "That record already exists (it would break a uniqueness rule).";
        }

        // CHANGED 6.2 (POPIA): a 5xx is a real failure, so it gets the full exception at Error.
        if (status >= 500)
        {
            logger.LogError(exception, "Request failed with status {Status}. CorrelationId: {CorrelationId}",
                status, correlationId);
        }
        else
        {
            // CHANGED 6.2 (POPIA): a 4xx is an expected answer. Log the exception TYPE only, never its message or stack.
            logger.LogInformation("Request rejected with status {Status} ({ExceptionType}). CorrelationId: {CorrelationId}",
                status, exception.GetType().Name, correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problemDetails.Extensions["correlationId"] = correlationId;   // same id as the log line and the trace

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails);
        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
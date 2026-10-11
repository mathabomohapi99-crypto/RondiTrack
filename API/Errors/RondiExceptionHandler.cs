using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Common;
using RondiTrack.Domain;
using System.Text.Json;

namespace RondiTrack.Errors;

public sealed class RondiExceptionHandler(ILogger<RondiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        var status = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var detail = "An unexpected error occurred.";

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

        logger.LogError(exception, "Request failed with status {Status}. CorrelationId: {CorrelationId}", status, correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails);
        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
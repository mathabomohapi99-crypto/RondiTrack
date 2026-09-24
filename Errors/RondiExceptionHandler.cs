using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
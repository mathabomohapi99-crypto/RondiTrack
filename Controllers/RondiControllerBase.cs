using Microsoft.AspNetCore.Mvc;
using RondiTrack.Domain;

namespace RondiTrack.Controllers;

[ApiController]
public abstract class RondiControllerBase : ControllerBase
{
    protected ObjectResult DomainProblem(DomainException ex)
    {
        var status = ex switch
        {
            DomainNotFoundException => StatusCodes.Status404NotFound,
            DomainConflictException => StatusCodes.Status409Conflict,
            DomainReferenceException => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(detail: ex.Message, statusCode: status);
    }

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);
}
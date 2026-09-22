using Microsoft.AspNetCore.Mvc;
using RondiTrack.Domain;

namespace RondiTrack.Controllers;

[ApiController]
public abstract class RondiControllerBase : ControllerBase
{
    protected ObjectResult DomainProblem(DomainException ex)
    {
        var status = ex is DomainConflictException
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;

        return Problem(detail: ex.Message, statusCode: status);
    }
}
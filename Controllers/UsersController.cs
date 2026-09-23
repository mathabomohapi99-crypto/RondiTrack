using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Controllers;

[Route("api/users")]
public class UsersController(IUserRepository users, IStokvelRepository stokvels) : RondiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll()
    {
        var all = await users.GetAllAsync();
        return Ok(all.Select(u => u.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await users.GetByIdAsync(id);
        return user is null ? NotFoundProblem($"User {id} was not found.") : Ok(user.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request)
    {
        try
        {
            var user = new User(request.FullName, request.Email);

            if (await users.EmailExistsAsync(user.Email))
                return Problem(detail: $"A user with email '{user.Email}' already exists.",
                    statusCode: StatusCodes.Status409Conflict);

            await users.AddAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToResponse());
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UserRequest request)
    {
        var user = await users.GetByIdAsync(id);
        if (user is null) return NotFoundProblem($"User {id} was not found.");

        try
        {
            if (await users.EmailExistsAsync(request.Email, excludingUserId: id))
                return Problem(detail: $"A user with email '{request.Email.Trim()}' already exists.",
                    statusCode: StatusCodes.Status409Conflict);

            user.UpdateDetails(request.FullName, request.Email);
            await users.UpdateAsync(user);
            return Ok(user.ToResponse());
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (await users.GetByIdAsync(id) is null) return NotFoundProblem($"User {id} was not found.");

        if (await stokvels.AnyWithMemberAsync(id))
            return Problem(detail: "This user belongs to at least one stokvel. Remove them from all stokvels first.",
                statusCode: StatusCodes.Status409Conflict);

        await users.DeleteAsync(id);
        return NoContent();
    }
}
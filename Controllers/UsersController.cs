using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Controllers;

[Route("api/users")]
public class UsersController(IUserRepository users, IStokvelRepository stokvels) : RondiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<User>>> GetAll() =>
        Ok(await users.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> GetById(Guid id)
    {
        var user = await users.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(UserRequest request)
    {
        try
        {
            var user = new User(request.FullName, request.Email);

            if (await users.EmailExistsAsync(user.Email))
                return Problem(detail: $"A user with email '{user.Email}' already exists.",
                    statusCode: StatusCodes.Status409Conflict);

            await users.AddAsync(user);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<User>> Update(Guid id, UserRequest request)
    {
        var user = await users.GetByIdAsync(id);
        if (user is null) return NotFound();

        try
        {
            if (await users.EmailExistsAsync(request.Email, excludingUserId: id))
                return Problem(detail: $"A user with email '{request.Email.Trim()}' already exists.",
                    statusCode: StatusCodes.Status409Conflict);

            user.UpdateDetails(request.FullName, request.Email);
            await users.UpdateAsync(user);
            return Ok(user);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (await users.GetByIdAsync(id) is null) return NotFound();

        if (await stokvels.AnyWithMemberAsync(id))
            return Problem(detail: "This user belongs to at least one stokvel. Remove them from all stokvels first.",
                statusCode: StatusCodes.Status409Conflict);

        await users.DeleteAsync(id);
        return NoContent();
    }
}
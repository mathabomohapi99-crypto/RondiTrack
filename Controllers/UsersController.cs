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
        var user = await users.GetByIdAsync(id) ?? throw new DomainNotFoundException($"User {id} was not found.");
        return Ok(user.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request)
    {
        var user = new User(request.FullName, request.Email);

        if (await users.EmailExistsAsync(user.Email))
            throw new DomainConflictException($"A user with email '{user.Email}' already exists.");

        await users.AddAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UserRequest request)
    {
        var user = await users.GetByIdAsync(id) ?? throw new DomainNotFoundException($"User {id} was not found.");

        if (await users.EmailExistsAsync(request.Email, excludingUserId: id))
            throw new DomainConflictException($"A user with email '{request.Email.Trim()}' already exists.");

        user.UpdateDetails(request.FullName, request.Email);
        await users.UpdateAsync(user);
        return Ok(user.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (await users.GetByIdAsync(id) is null)
            throw new DomainNotFoundException($"User {id} was not found.");

        if (await stokvels.AnyWithMemberAsync(id))
            throw new DomainConflictException("This user belongs to at least one stokvel. Remove them from all stokvels first.");

        await users.DeleteAsync(id);
        return NoContent();
    }
}
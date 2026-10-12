using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Controllers;

[Route("api/users")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]   // NEW 6.1
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]      // NEW 6.1
public class UsersController(IUserRepository users, IStokvelRepository stokvels) : RondiControllerBase
{
    /// <summary>Lists all users. Platform admins only.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.PlatformAdmin)]   // NEW 6.1: nobody else may list everyone's emails
    [ProducesResponseType(typeof(IReadOnlyList<UserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll()
    {
        var all = await users.GetAllAsync();
        return Ok(all.Select(u => u.ToResponse()));
    }

    /// <summary>Gets a user by id (your own account, or platform admin). The ETag header is the concurrency token.</summary>
    [HttpGet("{id:guid}")]
    [RequireUserSelf]   // NEW 6.1
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await users.GetByIdAsync(id) ?? throw new DomainNotFoundException($"User {id} was not found.");
        ETagHelper.Set(Response, user.Version);   // ADDED 5.3: token leaves the server
        return Ok(user.ToResponse());
    }

    /// <summary>Creates a user without a password. Platform admins only (people sign up with /api/auth/register).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.PlatformAdmin)]   // NEW 6.1
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request)
    {
        var user = new User(request.FullName, request.Email);

        if (await users.EmailExistsAsync(user.Email))
            throw new DomainConflictException("A user with this email already exists.");   // CHANGED 6.2: no email in the response

        await users.AddAsync(user);

        ETagHelper.Set(Response, user.Version);   // ADDED 5.3
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToResponse());
    }

    /// <summary>Updates a user's name and email (your own account, or platform admin). Requires If-Match.</summary>
    [HttpPut("{id:guid}")]
    [RequireUserSelf]   // NEW 6.1
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id, UserRequest request, [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        var expectedVersion = ETagHelper.ParseIfMatch(ifMatch);   // ADDED 5.3: the token is required

        var user = await users.GetByIdAsync(id) ?? throw new DomainNotFoundException($"User {id} was not found.");

        if (await users.EmailExistsAsync(request.Email, excludingUserId: id))
            throw new DomainConflictException("A user with this email already exists.");   // CHANGED 6.2: no email in the response

        user.UpdateDetails(request.FullName, request.Email);
        user.SetExpectedVersion(expectedVersion);   // ADDED 5.3: use the version the CLIENT saw
        await users.UpdateAsync(user);

        ETagHelper.Set(Response, user.Version);     // ADDED 5.3: hand back the new token
        return Ok(user.ToResponse());
    }

    /// <summary>Deletes a user (your own account, or platform admin). Fails if the user still belongs to any stokvel.</summary>
    [HttpDelete("{id:guid}")]
    [RequireUserSelf]   // NEW 6.1
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
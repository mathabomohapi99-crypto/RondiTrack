using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Controllers;

[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels, IUserRepository users) : RondiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Stokvel>>> GetAll() =>
        Ok(await stokvels.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Stokvel>> GetById(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        return stokvel is null ? NotFound() : Ok(stokvel);
    }

    [HttpPost]
    public async Task<ActionResult<Stokvel>> Create(StokvelRequest request)
    {
        try
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount,
                request.Frequency, request.MaxMembers);

            await stokvels.AddAsync(stokvel);
            return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, stokvel);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Stokvel>> Update(Guid id, StokvelRequest request)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        if (stokvel is null) return NotFound();

        try
        {
            stokvel.UpdateDetails(request.Name, request.ContributionAmount,
                request.Frequency, request.MaxMembers);

            await stokvels.UpdateAsync(stokvel);
            return Ok(stokvel);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await stokvels.DeleteAsync(id)) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<User>>> GetMembers(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        return stokvel is null ? NotFound() : Ok(stokvel.Members);
    }

    [HttpGet("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<User>> GetMember(Guid id, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        var member = stokvel?.FindMember(userId);
        return member is null ? NotFound() : Ok(member);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<User>> AddMember(Guid id, AddMemberRequest request)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        if (stokvel is null) return NotFound();

        var user = await users.GetByIdAsync(request.UserId);
        if (user is null)
            return Problem(detail: $"User {request.UserId} does not exist.",
                statusCode: StatusCodes.Status422UnprocessableEntity);

        try
        {
            stokvel.AddMember(user);
            await stokvels.UpdateAsync(stokvel);
            return CreatedAtAction(nameof(GetMember), new { id, userId = user.Id }, user);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        if (stokvel is null || !stokvel.RemoveMember(userId)) return NotFound();

        await stokvels.UpdateAsync(stokvel);
        return NoContent();
    }
}
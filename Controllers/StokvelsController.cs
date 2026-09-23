using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route("api/stokvels")]
public class StokvelsController(
    IStokvelRepository stokvels,
    IStokvelMembershipService membershipService,
    IContributionService contributionService) : RondiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll()
    {
        var all = await stokvels.GetAllAsync();
        return Ok(all.Select(s => s.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        return stokvel is null ? NotFoundProblem($"Stokvel {id} was not found.") : Ok(stokvel.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request)
    {
        try
        {
            var stokvel = new Stokvel(request.Name, request.ContributionAmount,
                request.Frequency, request.MaxMembers);

            await stokvels.AddAsync(stokvel);
            return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, stokvel.ToResponse());
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> Update(Guid id, StokvelRequest request)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        if (stokvel is null) return NotFoundProblem($"Stokvel {id} was not found.");

        try
        {
            stokvel.UpdateDetails(request.Name, request.ContributionAmount,
                request.Frequency, request.MaxMembers);

            await stokvels.UpdateAsync(stokvel);
            return Ok(stokvel.ToResponse());
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await stokvels.DeleteAsync(id)) return NotFoundProblem($"Stokvel {id} was not found.");
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetMembers(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        return stokvel is null
            ? NotFoundProblem($"Stokvel {id} was not found.")
            : Ok(stokvel.Members.Select(m => m.ToResponse()));
    }

    [HttpGet("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<UserResponse>> GetMember(Guid id, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        var member = stokvel?.FindMember(userId);
        return member is null ? NotFoundProblem($"User {userId} was not found in this stokvel.") : Ok(member.ToResponse());
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<UserResponse>> AddMember(Guid id, AddMemberRequest request)
    {
        try
        {
            var user = await membershipService.AddMemberAsync(id, request.UserId);
            return CreatedAtAction(nameof(GetMember), new { id, userId = user.Id }, user.ToResponse());
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        try
        {
            await membershipService.RemoveMemberAsync(id, userId);
            return NoContent();
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpGet("{id:guid}/contributions")]
    public async Task<ActionResult<StokvelResponse>> GetContributions(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id);
        return stokvel is null ? NotFoundProblem($"Stokvel {id} was not found.") : Ok(stokvel.ToResponse());
    }

    [HttpPost("{id:guid}/contributions")]
    public async Task<ActionResult<ContributionResponse>> RecordContribution(
        Guid id, ContributionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem(detail: "The Idempotency-Key header is required.",
                statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var (response, _) = await contributionService.RecordContributionAsync(id, request, idempotencyKey);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }
}
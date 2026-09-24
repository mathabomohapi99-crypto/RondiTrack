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
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return Ok(stokvel.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request)
    {
        var stokvel = new Stokvel(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        await stokvels.AddAsync(stokvel);
        return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, stokvel.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StokvelResponse>> Update(Guid id, StokvelRequest request)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        stokvel.UpdateDetails(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        await stokvels.UpdateAsync(stokvel);
        return Ok(stokvel.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await stokvels.DeleteAsync(id))
            throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetMembers(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return Ok(stokvel.Members.Select(m => m.ToResponse()));
    }

    [HttpGet("{id:guid}/members/{userId:guid}")]
    public async Task<ActionResult<UserResponse>> GetMember(Guid id, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        var member = stokvel.FindMember(userId) ?? throw new DomainNotFoundException($"User {userId} was not found in this stokvel.");
        return Ok(member.ToResponse());
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<UserResponse>> AddMember(Guid id, AddMemberRequest request)
    {
        var user = await membershipService.AddMemberAsync(id, request.UserId);
        return CreatedAtAction(nameof(GetMember), new { id, userId = user.Id }, user.ToResponse());
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await membershipService.RemoveMemberAsync(id, userId);
        return NoContent();
    }

    [HttpPost("{id:guid}/contributions")]
    public async Task<ActionResult<ContributionResponse>> RecordContribution(
        Guid id, ContributionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("The Idempotency-Key header is required.");

        var (response, _) = await contributionService.RecordContributionAsync(id, request, idempotencyKey);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
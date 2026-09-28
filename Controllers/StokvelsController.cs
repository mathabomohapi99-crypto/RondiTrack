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
    /// <summary>Lists all stokvels.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StokvelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll()
    {
        var all = await stokvels.GetAllAsync();
        return Ok(all.Select(s => s.ToResponse()));
    }

    /// <summary>Gets a single stokvel by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return Ok(stokvel.ToResponse());
    }

    /// <summary>Creates a new stokvel.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request)
    {
        var stokvel = new Stokvel(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        await stokvels.AddAsync(stokvel);
        return CreatedAtAction(nameof(GetById), new { id = stokvel.Id }, stokvel.ToResponse());
    }

    /// <summary>Updates a stokvel's details. Capacity cannot drop below the current member count.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StokvelResponse>> Update(Guid id, StokvelRequest request)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        stokvel.UpdateDetails(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        await stokvels.UpdateAsync(stokvel);
        return Ok(stokvel.ToResponse());
    }

    /// <summary>Deletes a stokvel.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await stokvels.DeleteAsync(id))
            throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return NoContent();
    }

    /// <summary>Lists a stokvel's members.</summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetMembers(Guid id)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        return Ok(stokvel.Members.Select(m => m.ToResponse()));
    }

    /// <summary>Gets a single member of a stokvel.</summary>
    [HttpGet("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetMember(Guid id, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(id) ?? throw new DomainNotFoundException($"Stokvel {id} was not found.");
        var member = stokvel.FindMember(userId) ?? throw new DomainNotFoundException($"User {userId} was not found in this stokvel.");
        return Ok(member.ToResponse());
    }

    /// <summary>Adds an existing user as a member. Fails if the stokvel is full or the user is already a member.</summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserResponse>> AddMember(Guid id, AddMemberRequest request)
    {
        var user = await membershipService.AddMemberAsync(id, request.UserId);
        return CreatedAtAction(nameof(GetMember), new { id, userId = user.Id }, user.ToResponse());
    }

    /// <summary>Removes a member from a stokvel.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await membershipService.RemoveMemberAsync(id, userId);
        return NoContent();
    }

    /// <summary>
    /// Records a member's contribution to a stokvel cycle. Requires an Idempotency-Key header;
    /// a repeated request with the same key and body returns the original result, the same key
    /// with a different body is rejected, and a member cannot pay for the same cycle twice.
    /// </summary>
    [HttpPost("{id:guid}/contributions")]
    [ProducesResponseType(typeof(ContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContributionResponse>> RecordContribution(
        Guid id, ContributionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("The Idempotency-Key header is required.");

        var (response, _) = await contributionService.RecordContributionAsync(id, request, idempotencyKey);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
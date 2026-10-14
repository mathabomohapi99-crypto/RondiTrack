using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/stokvels")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]   // NEW 6.1: documented on every action
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]      // NEW 6.1
public class StokvelsController(
    IStokvelRepository stokvels,
    IStokvelMembershipService membershipService,
    IContributionService contributionService,
    MemberPagingService memberPaging,
    IStokvelAccess access) : RondiControllerBase   // NEW 6.1: access = the 404/403 decision for the contribution check
{
    /// <summary>Lists all stokvels. Platform admins only.</summary>
    [HttpGet]
    [Authorize(Roles = AuthConstants.PlatformAdminRole)]   // NEW 6.1: the ONE plain role check in the whole API
    [ProducesResponseType(typeof(IReadOnlyList<StokvelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll()
    {
        var all = await stokvels.GetAllAsync();
        return Ok(all.Select(s => s.ToResponse()));
    }

    /// <summary>Gets a single stokvel by id. The ETag header is the concurrency token.</summary>
    [HttpGet("{stokvelId:guid}")]
    [RequireStokvelMember]   // NEW 6.1
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid stokvelId)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId) ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        ETagHelper.Set(Response, stokvel.Version);   // ADDED 5.3: token leaves the server
        return Ok(stokvel.ToResponse());
    }

    /// <summary>Creates a new stokvel. Any signed-in user (see README gap about the creator).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request)
    {
        var stokvel = new Stokvel(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        await stokvels.AddAsync(stokvel);

        ETagHelper.Set(Response, stokvel.Version);   // ADDED 5.3
        return CreatedAtAction(nameof(GetById), new { stokvelId = stokvel.Id }, stokvel.ToResponse());
    }

    /// <summary>Updates a stokvel's details. Treasurer or Admin only. Requires the If-Match header.</summary>
    [HttpPut("{stokvelId:guid}")]
    [RequireStokvelOfficer]   // NEW 6.1
    [ProducesResponseType(typeof(StokvelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<StokvelResponse>> Update(
        Guid stokvelId, StokvelRequest request, [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        var expectedVersion = ETagHelper.ParseIfMatch(ifMatch);   // ADDED 5.3: the token is required

        var stokvel = await stokvels.GetByIdAsync(stokvelId) ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        stokvel.UpdateDetails(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers);
        stokvel.SetExpectedVersion(expectedVersion);   // ADDED 5.3: use the version the CLIENT saw
        await stokvels.UpdateAsync(stokvel);

        ETagHelper.Set(Response, stokvel.Version);     // ADDED 5.3: hand back the new token
        return Ok(stokvel.ToResponse());
    }

    /// <summary>Deletes a stokvel. Treasurer or Admin only.</summary>
    [HttpDelete("{stokvelId:guid}")]
    [RequireStokvelOfficer]   // NEW 6.1
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid stokvelId)
    {
        if (!await stokvels.DeleteAsync(stokvelId))
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        return NoContent();
    }

    /// <summary>
    /// Lists a stokvel's members in rotation order. CHANGED 5.3: paged in the database.
    /// Query: pageSize, pageToken.
    /// </summary>
    [HttpGet("{stokvelId:guid}/members")]
    [RequireStokvelMember]   // NEW 6.1
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> GetMembers(
        Guid stokvelId, [FromQuery] int? pageSize, [FromQuery] string? pageToken, CancellationToken ct)
    {
        var result = await memberPaging.ListAsync(stokvelId, pageSize, pageToken, ct)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        return Ok(result);
    }

    /// <summary>Gets a single member of a stokvel.</summary>
    [HttpGet("{stokvelId:guid}/members/{userId:guid}")]
    [RequireStokvelMember]   // NEW 6.1
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetMember(Guid stokvelId, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId) ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        var member = stokvel.FindMember(userId) ?? throw new DomainNotFoundException($"User {userId} was not found in this stokvel.");
        return Ok(member.ToResponse());
    }

    /// <summary>Adds an existing user as a member. Treasurer or Admin only.</summary>
    [HttpPost("{stokvelId:guid}/members")]
    [RequireStokvelOfficer]   // NEW 6.1
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserResponse>> AddMember(Guid stokvelId, AddMemberRequest request)
    {
        var user = await membershipService.AddMemberAsync(stokvelId, request.UserId);
        return CreatedAtAction(nameof(GetMember), new { stokvelId, userId = user.Id }, user.ToResponse());
    }

    /// <summary>Removes a member from a stokvel. Treasurer or Admin only.</summary>
    [HttpDelete("{stokvelId:guid}/members/{userId:guid}")]
    [RequireStokvelOfficer]   // NEW 6.1
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid stokvelId, Guid userId)
    {
        await membershipService.RemoveMemberAsync(stokvelId, userId);
        return NoContent();
    }

    /// <summary>
    /// Records a contribution. A member may record their own; a Treasurer or Admin may record for anyone.
    /// Requires an Idempotency-Key header.
    /// </summary>
    [HttpPost("{stokvelId:guid}/contributions")]
    [EnableRateLimiting(RateLimitPolicies.Contribution)]
    [RequireStokvelMember]   // NEW 6.1: must at least be a member (non-members get 404 before anything else)
    [ProducesResponseType(typeof(ContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContributionResponse>> RecordContribution(
        Guid stokvelId, ContributionRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        // NEW 6.1: own contribution = ok, someone else's = officer only (403 otherwise)
        await access.EnsureAsync(User, stokvelId, Policies.RecordContribution, request.UserId);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("The Idempotency-Key header is required.");

        var (response, replayed) = await contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey);
        if (replayed) Response.Headers[ApiHeaders.IdempotencyReplayed] = "true";
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
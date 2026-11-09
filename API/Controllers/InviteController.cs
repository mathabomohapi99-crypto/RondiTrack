using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using RondiTrack.Web;

namespace RondiTrack.Controllers;

[Route(ApiRoutes.V1)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class InvitesController(IInviteService invites) : RondiControllerBase
{
    private Guid Me => User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");

    /// <summary>Makes a new invite code (valid 7 days). Treasurer or Admin of this stokvel only.</summary>
    [HttpPost("stokvels/{stokvelId:guid}/invites")]
    [RequireStokvelOfficer]
    [ProducesResponseType(typeof(InviteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InviteResponse>> Create(Guid stokvelId, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await invites.CreateAsync(stokvelId, Me, ct));

    /// <summary>What the invite is for: name, amount, schedule and member count. Nothing about the members.</summary>
    [HttpGet("invites/{code}")]
    [EnableRateLimiting(RateLimitPolicies.Invite)]
    [ProducesResponseType(typeof(InvitePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<InvitePreviewResponse>> Preview(string code, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await invites.PreviewAsync(code, Me, ct));
    }

    /// <summary>Joins the stokvel the code belongs to. Already a member = 409 already_member.</summary>
    [HttpPost("invites/{code}/join")]
    [EnableRateLimiting(RateLimitPolicies.Invite)]
    [ProducesResponseType(typeof(JoinInviteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<JoinInviteResponse>> Join(string code, CancellationToken ct) =>
        Ok(await invites.JoinAsync(code, Me, ct));
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using RondiTrack.Web;

namespace RondiTrack.Controllers;

// NEW 7.4: the contribution endpoint the React form uses (cycle in the URL, amount in integer cents).
[Route($"{ApiRoutes.V1}/stokvels")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public class CycleContributionsController(
    IContributionService contributionService,
    IStokvelAccess access) : RondiControllerBase
{
    /// <summary>
    /// Records a contribution in one cycle. Amount is integer cents. A member may record their own;
    /// a Treasurer or Admin may record for anyone. Requires an Idempotency-Key header.
    /// </summary>
    [HttpPost("{stokvelId:guid}/cycles/{cycleId:guid}/contributions")]
    [EnableRateLimiting(RateLimitPolicies.Contribution)]
    [RequireStokvelMember]
    [ProducesResponseType(typeof(ContributionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ContributionResponse>> RecordInCycle(
        Guid stokvelId,
        Guid cycleId,
        RecordContributionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        // Own contribution = ok, someone else's = officer only (403 otherwise). Same rule as the old endpoint.
        await access.EnsureAsync(User, stokvelId, Policies.RecordContribution, request.UserId);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainValidationException("The Idempotency-Key header is required.");

        // cents -> decimal is exact (decimal division), so no rounding happens here.
        var amount = request.AmountInCents / 100m;

        var (response, replayed) = await contributionService.RecordContributionAsync(
            stokvelId, new ContributionRequest(request.UserId, cycleId, amount), idempotencyKey);

        if (replayed) Response.Headers[ApiHeaders.IdempotencyReplayed] = "true";
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
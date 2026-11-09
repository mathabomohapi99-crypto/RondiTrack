using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using RondiTrack.Web;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/stokvels/{{stokvelId:guid}}/cycles/{{cycleId:guid}}")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public class CycleCloseController(ICycleCloseService closer) : RondiControllerBase
{
    /// <summary>What closing this cycle will do, worked out now by the API. Treasurer or Admin only.</summary>
    [HttpGet("close-preview")]
    [RequireStokvelOfficer]
    [ProducesResponseType(typeof(ClosePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClosePreviewResponse>> Preview(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await closer.PreviewAsync(stokvelId, cycleId, ct));
    }

    /// <summary>
    /// Closes the cycle and records the arrears, but only if the version still matches the preview (else 409 stale_preview).
    /// Needs an Idempotency-Key: the same key closes once and gets the same answer again.
    /// </summary>
    [HttpPost("close")]
    [EnableRateLimiting(RateLimitPolicies.Payout)]
    [RequireStokvelOfficer]
    [ProducesResponseType(typeof(CloseCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CloseCycleResponse>> Close(
        Guid stokvelId, Guid cycleId, CloseCycleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new DomainValidationException("The Idempotency-Key header is required (at most 200 characters).");
        if (string.IsNullOrWhiteSpace(request.Version) || request.Version.Length > 64)
            throw new DomainValidationException("version is required.");

        var (response, replayed) = await closer.CloseAsync(stokvelId, cycleId, request.Version, idempotencyKey, ct);
        if (replayed) Response.Headers[ApiHeaders.IdempotencyReplayed] = "true";
        return Ok(response);
    }
}
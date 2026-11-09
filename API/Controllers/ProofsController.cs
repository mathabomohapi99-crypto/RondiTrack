using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/stokvels/{{stokvelId:guid}}")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class ProofsController(IProofService proofs) : RondiControllerBase
{
    private Guid Me => User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");

    /// <summary>Attaches (or replaces) the proof of payment. Multipart form, field "file". Images or PDF, up to 5 MB.</summary>
    [HttpPut("contributions/{contributionId:guid}/proof")]
    [RequestSizeLimit(ProofRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProofResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ProofResponse>> Upload(
        Guid stokvelId, Guid contributionId, IFormFile? file, CancellationToken ct) =>
        Ok(await proofs.SaveAsync(Me, stokvelId, contributionId, file, ct));

    /// <summary>Downloads the proof as an attachment. Not allowed = the same 404 as a missing file.</summary>
    [HttpGet("contributions/{contributionId:guid}/proof")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        var file = await proofs.GetAsync(Me, stokvelId, contributionId, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";   // the browser must not guess another type
        Response.Headers.CacheControl = "private, no-store";      // never kept by a shared cache
        return File(file.Data, file.ContentType, file.DownloadName);   // file name makes it an attachment
    }

    /// <summary>Removes the proof. Refused (409) once the cycle is closed.</summary>
    [HttpDelete("contributions/{contributionId:guid}/proof")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        await proofs.DeleteAsync(Me, stokvelId, contributionId, ct);
        return NoContent();
    }

    /// <summary>Which contributions have a proof I may open (optional ?cycleId=). Used to show download links only where they work.</summary>
    [HttpGet("proofs")]
    [ProducesResponseType(typeof(IReadOnlyList<ProofResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProofResponse>>> List(Guid stokvelId, [FromQuery] Guid? cycleId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await proofs.ListAsync(Me, stokvelId, cycleId, ct));
    }
}
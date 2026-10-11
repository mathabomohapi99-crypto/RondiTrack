using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payouts")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]   // NEW 6.1
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]      // NEW 6.1
public sealed class PayoutsController(IPayoutService payouts) : ControllerBase
{
    /// <summary>Processes the next payout for a cycle. Treasurer or Admin only.</summary>
    [HttpPost]
    [RequireStokvelOfficer]   // NEW 6.1: moving money needs an officer of THIS stokvel
    [ProducesResponseType(typeof(PayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutResponse>> ProcessNextPayout(
        Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var result = await payouts.ProcessNextPayoutAsync(stokvelId, cycleId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payouts")]
public sealed class PayoutsController(IPayoutService payouts) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PayoutResponse>> ProcessNextPayout(
        Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var result = await payouts.ProcessNextPayoutAsync(stokvelId, cycleId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
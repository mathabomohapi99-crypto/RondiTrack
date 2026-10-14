using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/me")]
public sealed class MeController(IHomeService home) : RondiControllerBase
{
    /// <summary>One response for the member's home screen: every stokvel they belong to.</summary>
    [HttpGet("home")]
    [ProducesResponseType(typeof(HomeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken ct) =>
        Ok(await home.GetAsync(User.GetUserId()!.Value, ct));
}

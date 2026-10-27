using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

// NEW 8.1: endpoints about the signed-in person. No id in the URL, so nobody can ask about someone else.
[Route("api/me")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class MeController(IUserRepository users, MyStokvelsService myStokvels) : RondiControllerBase
{
    /// <summary>Who am I? Returns the signed-in user's id, name and email.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        var userId = User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");
        var user = await users.GetByIdAsync(userId) ?? throw new DomainNotFoundException("User was not found.");
        return Ok(new MeResponse(user.Id, user.FullName, user.Email));
    }

    /// <summary>Lists every stokvel I belong to, with MY role in each. No stokvels = 200 with an empty list.</summary>
    [HttpGet("stokvels")]
    [ProducesResponseType(typeof(IReadOnlyList<MyStokvelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyStokvelResponse>>> GetMyStokvels(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");
        return Ok(await myStokvels.ListMineAsync(userId, ct));
    }
}
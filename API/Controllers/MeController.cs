using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/me")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class MeController(IHomeService home, IUserRepository users, MyStokvelsService myStokvels) : RondiControllerBase
{
    /// <summary>One response for the member's home screen: every stokvel they belong to.</summary>
    [HttpGet("home")]
    [ProducesResponseType(typeof(HomeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeResponse>> Home(CancellationToken ct) =>
        Ok(await home.GetAsync(User.GetUserId()!.Value, ct));

    /// <summary>Who am I? Returns the signed-in user's id, name and email.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeResponse>> GetMe()
    {
        var userId = User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");
        var user = await users.GetByIdAsync(userId) ?? throw new DomainNotFoundException("User was not found.");
        return Ok(new MeResponse(user.Id, user.FullName, user.Email));
    }

    /// <summary>Every stokvel I belong to, with MY role in each. No stokvels = 200 with an empty list.</summary>
    [HttpGet("stokvels")]
    [ProducesResponseType(typeof(IReadOnlyList<MyStokvelResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyStokvelResponse>>> GetMyStokvels(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");
        return Ok(await myStokvels.ListMineAsync(userId, ct));
    }
}
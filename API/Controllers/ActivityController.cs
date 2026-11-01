using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/me/activity")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public sealed class ActivityController(ActivityFeedService feed) : RondiControllerBase
{
    /// <summary>NEW 8.3: recent activity across the stokvels I belong to, newest first. Query: pageSize, pageToken.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ActivityItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ActivityItemResponse>>> List(
        [FromQuery] int? pageSize, [FromQuery] string? pageToken, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new DomainNotFoundException("User was not found.");
        return Ok(await feed.ListAsync(userId, pageSize, pageToken, ct));
    }
}
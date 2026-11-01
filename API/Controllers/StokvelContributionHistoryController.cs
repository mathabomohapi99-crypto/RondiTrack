using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Auth;
using RondiTrack.Common;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/stokvels")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public class StokvelContributionHistoryController(StokvelContributionPagingService paging) : RondiControllerBase
{
    /// <summary>
    /// NEW 8.3: contribution history of one stokvel. Query: pageSize, pageToken, sort, member, from (included), to (excluded).
    /// from/to are UTC instants. Non-members get 404.
    /// </summary>
    [HttpGet("{stokvelId:guid}/contributions")]
    [RequireStokvelMember]
    [ProducesResponseType(typeof(PagedResponse<ContributionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ContributionDetailResponse>>> List(
        Guid stokvelId, [FromQuery] StokvelContributionListQuery query, CancellationToken ct) =>
        Ok(await paging.ListAsync(stokvelId, query, ct));
}
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Common.Paging;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class ContributionCyclesController(
    IContributionCycleRepository cycles,
    IStokvelRepository stokvels,
    ContributionPagingService contributionPaging) : RondiControllerBase   // CHANGED 5.3: paging service replaces the strategy switch
{
    /// <summary>Lists a stokvel's contribution cycles.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContributionCycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ContributionCycleResponse>>> GetAll(Guid stokvelId)
    {
        if (await stokvels.GetByIdAsync(stokvelId) is null)
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var all = await cycles.GetByStokvelAsync(stokvelId);
        return Ok(all.Select(c => c.ToResponse()));
    }

    /// <summary>Gets a single contribution cycle. The ETag header is the concurrency token.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContributionCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid id)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");

        ETagHelper.Set(Response, cycle.Version);   // ADDED 5.3: token leaves the server
        return Ok(cycle.ToResponse());
    }

    /// <summary>
    /// Lists the contributions recorded for a cycle, with each member's details.
    /// CHANGED 5.3: paged, sortable and filterable. All of it runs in the database.
    /// Query: pageSize, pageToken, sort (date|-date|amount|-amount), userId, dateFrom, dateTo.
    /// </summary>
    [HttpGet("{cycleId:guid}/contributions")]
    [ProducesResponseType(typeof(PagedResponse<ContributionDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ContributionDetailResponse>>> GetContributions(
        Guid stokvelId, Guid cycleId, [FromQuery] ContributionListQuery query, CancellationToken ct)
    {
        var cycle = await cycles.GetByIdAsync(cycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {cycleId} was not found.");

        return Ok(await contributionPaging.ListAsync(cycleId, query, ct));
    }

    /// <summary>Creates a new contribution cycle for a stokvel.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContributionCycleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionCycleResponse>> Create(Guid stokvelId, ContributionCycleRequest request)
    {
        if (await stokvels.GetByIdAsync(stokvelId) is null)
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var cycle = new ContributionCycle(stokvelId, request.CycleNumber, request.TargetAmount);
        await cycles.AddAsync(cycle);

        ETagHelper.Set(Response, cycle.Version);   // ADDED 5.3
        return CreatedAtAction(nameof(GetById), new { stokvelId, id = cycle.Id }, cycle.ToResponse());
    }

    /// <summary>Updates a cycle's number and target amount. Requires the If-Match header (ETag from GET).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContributionCycleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<ContributionCycleResponse>> Update(
        Guid stokvelId, Guid id, ContributionCycleRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        var expectedVersion = ETagHelper.ParseIfMatch(ifMatch);   // ADDED 5.3: the token is required

        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");

        cycle.UpdateDetails(request.CycleNumber, request.TargetAmount);
        cycle.SetExpectedVersion(expectedVersion);   // ADDED 5.3: use the version the CLIENT saw, not the one just loaded
        await cycles.UpdateAsync(cycle);

        ETagHelper.Set(Response, cycle.Version);     // ADDED 5.3: hand back the new token
        return Ok(cycle.ToResponse());
    }

    /// <summary>Deletes a contribution cycle.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid id)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");

        await cycles.DeleteAsync(id);
        return NoContent();
    }
}
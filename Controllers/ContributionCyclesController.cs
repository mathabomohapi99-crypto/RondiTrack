using Microsoft.AspNetCore.Mvc;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Controllers;

[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class ContributionCyclesController(
    IContributionCycleRepository cycles,
    IStokvelRepository stokvels) : RondiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionCycleResponse>>> GetAll(Guid stokvelId)
    {
        if (await stokvels.GetByIdAsync(stokvelId) is null)
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var all = await cycles.GetByStokvelAsync(stokvelId);
        return Ok(all.Select(c => c.ToResponse()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid id)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");
        return Ok(cycle.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<ContributionCycleResponse>> Create(Guid stokvelId, ContributionCycleRequest request)
    {
        if (await stokvels.GetByIdAsync(stokvelId) is null)
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var cycle = new ContributionCycle(stokvelId, request.CycleNumber, request.TargetAmount);
        await cycles.AddAsync(cycle);
        return CreatedAtAction(nameof(GetById), new { stokvelId, id = cycle.Id }, cycle.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContributionCycleResponse>> Update(Guid stokvelId, Guid id, ContributionCycleRequest request)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");

        cycle.UpdateDetails(request.CycleNumber, request.TargetAmount);
        await cycles.UpdateAsync(cycle);
        return Ok(cycle.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid id)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainNotFoundException($"Cycle {id} was not found.");

        await cycles.DeleteAsync(id);
        return NoContent();
    }
}
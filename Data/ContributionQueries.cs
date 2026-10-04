using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Data;

// The naive N+1 version and the two fixes, side by side
public interface IContributionQueries
{
    Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleNaiveAsync(Guid stokvelId, Guid cycleId);
    Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleIncludeAsync(Guid stokvelId, Guid cycleId);
    Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleProjectedAsync(Guid stokvelId, Guid cycleId);
}

public sealed class ContributionQueries(RondiTrackDbContext db) : IContributionQueries
{
    // VERSION 1: deliberately NAIVE. 1 query for the rows, then 2 more queries PER ROW.
    public async Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleNaiveAsync(Guid stokvelId, Guid cycleId)
    {
        var rows = await db.Contributions
            .Where(c => c.StokvelId == stokvelId && c.CycleId == cycleId)
            .OrderBy(c => c.RecordedAtUtc)
            .ToListAsync();

        var result = new List<ContributionDetailResponse>();
        foreach (var c in rows)
        {
            await db.Entry(c).Reference(x => x.Member).LoadAsync();        // one round trip per row
            await db.Entry(c.Member).Reference(m => m.User).LoadAsync();   // another round trip per row
            result.Add(ToDto(c));
        }
        return result;
    }

    // VERSION 2: FIX with eager loading. One query with joins, pulls the whole graph.
    public async Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleIncludeAsync(Guid stokvelId, Guid cycleId)
    {
        var rows = await db.Contributions
            .AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.CycleId == cycleId)
            .Include(c => c.Member).ThenInclude(m => m.User)
            .OrderBy(c => c.RecordedAtUtc)
            .ToListAsync();

        return rows.Select(ToDto).ToList();
    }

    // VERSION 3: FIX with projection. One query, only the columns the response needs. THIS ONE SHIPS.
    public async Task<IReadOnlyList<ContributionDetailResponse>> GetForCycleProjectedAsync(Guid stokvelId, Guid cycleId) =>
        await db.Contributions
            .AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.CycleId == cycleId)
            .OrderBy(c => c.RecordedAtUtc)
            .Select(c => new ContributionDetailResponse(
                c.Id, c.UserId, c.Member.User.FullName, c.Member.User.Email,
                c.Member.Role, c.Member.RotationPosition, c.Amount, c.RecordedAtUtc))
            .ToListAsync();

    private static ContributionDetailResponse ToDto(Contribution c) =>
        new(c.Id, c.UserId, c.Member.User.FullName, c.Member.User.Email,
            c.Member.Role, c.Member.RotationPosition, c.Amount, c.RecordedAtUtc);
}
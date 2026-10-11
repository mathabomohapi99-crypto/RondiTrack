using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

public sealed class EfContributionRepository(RondiTrackDbContext db) : IContributionRepository
{
    public Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid cycleId) =>
        db.Contributions.AnyAsync(c =>
            c.StokvelId == stokvelId && c.UserId == userId && c.CycleId == cycleId);

    public async Task AddAsync(Contribution contribution)
    {
        db.Contributions.Add(contribution);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId) =>
        await db.Contributions
            .AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.RecordedAtUtc)
            .ToListAsync();
}
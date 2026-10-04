using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

// New file. Dedicated access for StokvelMember (composite key = two ids)
public interface IStokvelMemberRepository
{
    // forUpdate: false = pure read (not tracked), true = I will change it and save
    Task<StokvelMember?> GetAsync(Guid userId, Guid stokvelId, bool forUpdate = false);
    Task<IReadOnlyList<StokvelMember>> GetByStokvelAsync(Guid stokvelId);
    Task AddAsync(StokvelMember member);
    Task RemoveAsync(StokvelMember member);
}

public sealed class EfStokvelMemberRepository(RondiTrackDbContext db) : IStokvelMemberRepository
{
    public async Task<StokvelMember?> GetAsync(Guid userId, Guid stokvelId, bool forUpdate = false)
    {
        IQueryable<StokvelMember> query = db.StokvelMembers;
        if (!forUpdate)
            query = query.AsNoTracking();   // pure read: cheaper, nothing to save

        return await query.FirstOrDefaultAsync(m => m.UserId == userId && m.StokvelId == stokvelId);
    }

    public async Task<IReadOnlyList<StokvelMember>> GetByStokvelAsync(Guid stokvelId) =>
        await db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.StokvelId == stokvelId)
            .OrderBy(m => m.RotationPosition)
            .ToListAsync();

    public async Task AddAsync(StokvelMember member)
    {
        db.StokvelMembers.Add(member);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(StokvelMember member)
    {
        db.StokvelMembers.Remove(member);
        await db.SaveChangesAsync();
    }
}
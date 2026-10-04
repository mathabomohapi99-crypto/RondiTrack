using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

// New file. Users, Stokvels and cycles move from in-memory to Postgres,
// because the new foreign keys need real rows on both sides.

public sealed class EfUserRepository(RondiTrackDbContext db) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync() =>
        await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();

    public async Task<User?> GetByIdAsync(Guid id) =>
        await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var query = db.Users.Where(u => u.Email == normalized);
        if (excludingUserId is Guid excluded)
            query = query.Where(u => u.Id != excluded);
        return query.AnyAsync();
    }

    public async Task AddAsync(User user)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    // Reads are untracked, so a write attaches the changed object explicitly with Update()
    public async Task UpdateAsync(User user)
    {
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync() > 0;
}

public sealed class EfContributionCycleRepository(RondiTrackDbContext db) : IContributionCycleRepository
{
    public async Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId) =>
        await db.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.CycleNumber)
            .ToListAsync();

    public async Task<ContributionCycle?> GetByIdAsync(Guid id) =>
        await db.ContributionCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(ContributionCycle cycle)
    {
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(ContributionCycle cycle)
    {
        db.ContributionCycles.Update(cycle);
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        await db.ContributionCycles.Where(c => c.Id == id).ExecuteDeleteAsync() > 0;
}

public sealed class EfStokvelRepository(RondiTrackDbContext db) : IStokvelRepository
{
    // Eager loading: 2 queries in total, however many stokvels there are
    public async Task<IReadOnlyList<Stokvel>> GetAllAsync()
    {
        var stokvels = await db.Stokvels.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        var memberships = await db.StokvelMembers.AsNoTracking()
            .Include(m => m.User)
            .OrderBy(m => m.RotationPosition)
            .ToListAsync();

        foreach (var s in stokvels)
            s.LoadMembers(memberships.Where(m => m.StokvelId == s.Id).Select(m => m.User));

        return stokvels;
    }

    // Explicit loading: the stokvel first, then its members in a second, deliberate query
    public async Task<Stokvel?> GetByIdAsync(Guid id)
    {
        var stokvel = await db.Stokvels.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (stokvel is null) return null;

        var users = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == id)
            .OrderBy(m => m.RotationPosition)
            .Select(m => m.User)
            .ToListAsync();

        stokvel.LoadMembers(users);
        return stokvel;
    }

    public Task<bool> AnyWithMemberAsync(Guid userId) =>
        db.StokvelMembers.AnyAsync(m => m.UserId == userId);

    public async Task AddAsync(Stokvel stokvel)
    {
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();
    }

    // Saves the stokvel's own columns AND turns the in-memory member list into StokvelMember rows
    public async Task UpdateAsync(Stokvel stokvel)
    {
        db.Stokvels.Update(stokvel);

        var existing = await db.StokvelMembers.Where(m => m.StokvelId == stokvel.Id).ToListAsync();
        var wantedIds = stokvel.Members.Select(u => u.Id).ToHashSet();

        foreach (var gone in existing.Where(m => !wantedIds.Contains(m.UserId)))
            db.StokvelMembers.Remove(gone);

        var haveIds = existing.Select(m => m.UserId).ToHashSet();
        var nextPosition = existing.Count == 0 ? 1 : existing.Max(m => m.RotationPosition) + 1;

        foreach (var user in stokvel.Members.Where(u => !haveIds.Contains(u.Id)))
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, user.Id, nextPosition++));

        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id) =>
        await db.Stokvels.Where(s => s.Id == id).ExecuteDeleteAsync() > 0;
}
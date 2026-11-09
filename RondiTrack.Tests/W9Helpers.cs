using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Tests;

// NEW 9.1: small helpers so each test builds its own data
public static class W9
{
    public static async Task<Guid> AddContributionAsync(Guid stokvelId, Guid userId, Guid cycleId, decimal amount)
    {
        await using var db = TestDb.NewDb();
        var c = new Contribution(stokvelId, userId, cycleId, amount);
        db.Contributions.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    public static async Task CloseCycleDirectlyAsync(Guid cycleId)
    {
        await using var db = TestDb.NewDb();
        await db.ContributionCycles.Where(c => c.Id == cycleId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)DateTime.UtcNow));
    }

    // TestSeed makes MaxMembers = number of members (full). This opens room so someone can join.
    public static async Task MakeRoomAsync(Guid stokvelId)
    {
        await using var db = TestDb.NewDb();
        await db.Stokvels.Where(s => s.Id == stokvelId).ExecuteUpdateAsync(s => s.SetProperty(x => x.MaxMembers, 10));
    }
}
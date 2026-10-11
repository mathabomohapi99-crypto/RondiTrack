using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Tests;

// NEW 5.3: helpers so tests can talk to PostgreSQL directly with their own DbContexts.
public static class TestDb
{
    // CHANGED 5.4: the factory sets this to the Testcontainers connection string
    public static string? ConnectionString { get; set; }

    // Every call returns a brand new, independent DbContext (own connection, own tracking).
    public static RondiTrackDbContext NewDb()
    {
        // CHANGED 5.4: no more user-secrets, so tests can never touch the dev database
        var connectionString = ConnectionString
            ?? throw new InvalidOperationException("Test container is not started. Add [Collection(\"Postgres collection\")] to the test class.");

        var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(connectionString, n => n.EnableRetryOnFailure(4, TimeSpan.FromSeconds(10), null))
            .Options;

        return new RondiTrackDbContext(options);
    }

    // A brand new stokvel + user + member + cycle, so a test never depends on leftover data.
    public static async Task<(Guid StokvelId, Guid UserId, Guid CycleId)> SeedMemberAndCycleAsync()
    {
        await using var db = NewDb();

        var stokvel = new Stokvel($"Test {Guid.NewGuid()}", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("Test Member", $"t-{Guid.NewGuid()}@example.com");
        db.Users.Add(user);
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();                 // parents first

        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        db.StokvelMembers.Add(new StokvelMember(stokvel.Id, user.Id, 1));
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();

        return (stokvel.Id, user.Id, cycle.Id);
    }

    public static async Task<Guid> SeedPayoutAsync()
    {
        var (stokvelId, userId, cycleId) = await SeedMemberAndCycleAsync();
        await using var db = NewDb();
        var payout = new Payout(stokvelId, cycleId, userId, 100m);
        db.Payouts.Add(payout);
        await db.SaveChangesAsync();
        return payout.Id;
    }
}
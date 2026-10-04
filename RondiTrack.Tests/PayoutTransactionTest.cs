using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Services;

namespace RondiTrack.Tests;

public class PayoutTransactionTests
{
    // A hook that throws between the two writes, to force a failure mid-transaction
    private sealed class ThrowingHook : IPayoutFaultHook
    {
        public void AfterPayoutInserted() =>
            throw new InvalidOperationException("Simulated failure after the payout insert");
    }

    private static RondiTrackDbContext NewDb()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .Build();

        var connectionString = config.GetConnectionString("RondiTrack")
            ?? throw new InvalidOperationException("ConnectionStrings:RondiTrack is not set in user secrets.");

        var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql(connectionString, n => n.EnableRetryOnFailure(4, TimeSpan.FromSeconds(10), null))
            .Options;

        return new RondiTrackDbContext(options);
    }

    // EDIT 5.2: with real foreign keys the parents (users, stokvel) must be saved BEFORE members, cycle and contributions.
    // It now returns the first member's UserId (StokvelMember has no Id any more).
    private static async Task<(Guid StokvelId, Guid CycleId, Guid FirstUserId)> SeedAsync()
    {
        using var db = NewDb();

        var stokvel = new Stokvel($"Payout Test {Guid.NewGuid()}", 100m, ContributionFrequency.Monthly, 5);
        var user1 = new User("Payout One", $"p1-{Guid.NewGuid()}@example.com");
        var user2 = new User("Payout Two", $"p2-{Guid.NewGuid()}@example.com");
        var cycle = new ContributionCycle(stokvel.Id, 1, 200m);

        db.Users.AddRange(user1, user2);
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();                       // parents first

        db.StokvelMembers.AddRange(
            new StokvelMember(stokvel.Id, user1.Id, 1),
            new StokvelMember(stokvel.Id, user2.Id, 2));
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();                       // then members and cycle

        db.Contributions.AddRange(
            new Contribution(stokvel.Id, user1.Id, cycle.Id, 100m),
            new Contribution(stokvel.Id, user2.Id, cycle.Id, 100m));
        await db.SaveChangesAsync();                       // then contributions

        return (stokvel.Id, cycle.Id, user1.Id);
    }

    [Fact]
    public async Task ProcessNextPayout_WhenItFailsAfterThePayoutInsert_LeavesNothingBehind()
    {
        var (stokvelId, cycleId, _) = await SeedAsync();

        using (var db = NewDb())
        {
            // EDIT 5.2: PayoutService now takes the member repository too
            var service = new PayoutService(db, new ThrowingHook(), new EfStokvelMemberRepository(db));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ProcessNextPayoutAsync(stokvelId, cycleId));
        }

        // Re-query with a brand new DbContext: the database is the evidence, not a status code
        using var verify = NewDb();
        Assert.Equal(0, await verify.Payouts.CountAsync(p => p.CycleId == cycleId));

        var cycle = await verify.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        Assert.Equal(CycleStatus.Open, cycle.Status);
    }

    [Fact]
    public async Task ProcessNextPayout_PaysTheFirstMemberInRotationAndMarksTheCyclePaidOut()
    {
        var (stokvelId, cycleId, firstUserId) = await SeedAsync();

        using (var db = NewDb())
        {
            var result = await new PayoutService(db, new NoOpPayoutFaultHook(), new EfStokvelMemberRepository(db))
                .ProcessNextPayoutAsync(stokvelId, cycleId);

            Assert.Equal(firstUserId, result.RecipientUserId);   // EDIT 5.2: was RecipientMemberId
            Assert.Equal(200m, result.Amount);
        }

        using var verify = NewDb();
        Assert.Equal(1, await verify.Payouts.CountAsync(p => p.CycleId == cycleId));

        var cycle = await verify.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        Assert.Equal(CycleStatus.PaidOut, cycle.Status);
    }
}
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

    // Fresh GUIDs every time, so tests never collide with rows left by earlier runs
    private static async Task<(Guid StokvelId, Guid CycleId, Guid FirstMemberId)> SeedAsync()
    {
        using var db = NewDb();

        var stokvelId = Guid.NewGuid();
        var cycle = new ContributionCycle(stokvelId, 1, 200m);
        var first = new StokvelMember(stokvelId, Guid.NewGuid(), 1);
        var second = new StokvelMember(stokvelId, Guid.NewGuid(), 2);

        db.ContributionCycles.Add(cycle);
        db.StokvelMembers.AddRange(first, second);
        db.Contributions.AddRange(
            new Contribution(stokvelId, first.UserId, cycle.Id, 100m),
            new Contribution(stokvelId, second.UserId, cycle.Id, 100m));

        await db.SaveChangesAsync();
        return (stokvelId, cycle.Id, first.Id);
    }

    [Fact]
    public async Task ProcessNextPayout_WhenItFailsAfterThePayoutInsert_LeavesNothingBehind()
    {
        var (stokvelId, cycleId, _) = await SeedAsync();

        using (var db = NewDb())
        {
            var service = new PayoutService(db, new ThrowingHook());
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
        var (stokvelId, cycleId, firstMemberId) = await SeedAsync();

        using (var db = NewDb())
        {
            var result = await new PayoutService(db, new NoOpPayoutFaultHook())
                .ProcessNextPayoutAsync(stokvelId, cycleId);

            Assert.Equal(firstMemberId, result.RecipientMemberId);
            Assert.Equal(200m, result.Amount);
        }

        using var verify = NewDb();
        Assert.Equal(1, await verify.Payouts.CountAsync(p => p.CycleId == cycleId));

        var cycle = await verify.ContributionCycles.SingleAsync(c => c.Id == cycleId);
        Assert.Equal(CycleStatus.PaidOut, cycle.Status);
    }
}
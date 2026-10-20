using RondiTrack.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Jobs;

namespace RondiTrack.Tests;

// 6.4: cycle-close job. Every test builds its own stokvel and passes ITS id to RunOnceAsync, so tests never touch each other's data.
[Collection("Postgres collection")]
public class CycleCloseTests(PostgresApiFactory factory)
{
    // SAST = UTC+2 all year (no daylight saving)
    private static DateTimeOffset Sast(int y, int m, int d, int h, int min) =>
        new(new DateTime(y, m, d, h, min, 0), TimeSpan.FromHours(2));

    private (CycleCloseJob Job, FakeTimeProvider Clock) MakeJob(DateTimeOffset now)
    {
        var clock = new FakeTimeProvider(now);
        var f = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(clock);
        }));
        return (f.Services.GetRequiredService<CycleCloseJob>(), clock);
    }

    private async Task<Guid[]> UsersAsync(int n)
    {
        var ids = new Guid[n];
        for (var i = 0; i < n; i++) ids[i] = await TestSeed.UserAsync(factory.Services);
        return ids;
    }

    private async Task PayAsync(Guid stokvelId, Guid userId, Guid cycleId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        db.Contributions.Add(new Contribution(stokvelId, userId, cycleId, amount));
        await db.SaveChangesAsync();
    }

    private async Task<(List<ContributionCycle> Cycles, List<Arrear> Arrears)> StateAsync(Guid stokvelId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var cycles = await db.ContributionCycles.AsNoTracking().Where(c => c.StokvelId == stokvelId).OrderBy(c => c.CycleNumber).ToListAsync();
        var arrears = await db.Arrears.AsNoTracking().Where(a => a.StokvelId == stokvelId).ToListAsync();
        return (cycles, arrears);
    }

    [Fact]
    public async Task Ended_cycle_is_closed_underpayers_get_arrears_in_cents_and_next_cycle_exists()
    {
        var u = await UsersAsync(3);
        var endsOn = new DateOnly(2026, 3, 25);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member), (u[2], StokvelRole.Member)], 1, 500m, endsOn);
        await PayAsync(stokvelId, u[0], cycles[0], 500m);   // paid in full
        await PayAsync(stokvelId, u[1], cycles[0], 200m);   // short by R300
        // u[2] paid nothing: short by R500

        var (job, _) = MakeJob(Sast(2026, 3, 26, 0, 0));
        await job.RunOnceAsync([stokvelId]);

        var (all, arrears) = await StateAsync(stokvelId);
        Assert.Equal(2, all.Count);
        Assert.NotNull(all[0].ClosedAtUtc);
        Assert.Equal(2, all[1].CycleNumber);
        Assert.Null(all[1].ClosedAtUtc);
        Assert.Equal(endsOn.AddMonths(1), all[1].EndsOn);
        Assert.Equal(2, arrears.Count);
        Assert.Equal(30000, arrears.Single(a => a.UserId == u[1]).ShortfallCents);
        Assert.Equal(50000, arrears.Single(a => a.UserId == u[2]).ShortfallCents);
        Assert.DoesNotContain(arrears, a => a.UserId == u[0]);   // paid in full = no arrear
    }

    [Fact]
    public async Task Cycle_that_ends_tomorrow_is_not_touched()
    {
        var u = await UsersAsync(2);
        var (stokvelId, _) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member)], 1, 500m, new DateOnly(2026, 3, 26));

        var (job, _) = MakeJob(Sast(2026, 3, 25, 12, 0));   // "today" is the 25th, cycle ends on the 26th
        await job.RunOnceAsync([stokvelId]);

        var (all, arrears) = await StateAsync(stokvelId);
        Assert.Single(all);
        Assert.Null(all[0].ClosedAtUtc);
        Assert.Empty(arrears);
    }

    [Fact]
    public async Task Time_zone_boundary_stays_open_at_2359_SAST_and_closes_at_0000_SAST()
    {
        var u = await UsersAsync(2);
        var (stokvelId, _) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member)], 1, 500m, new DateOnly(2026, 3, 25));

        var (job, clock) = MakeJob(Sast(2026, 3, 25, 23, 59));   // = 21:59 UTC
        await job.RunOnceAsync([stokvelId]);
        Assert.Null((await StateAsync(stokvelId)).Cycles[0].ClosedAtUtc);   // still open

        clock.SetUtcNow(Sast(2026, 3, 26, 0, 0));                // = 22:00 UTC on the 25th
        await job.RunOnceAsync([stokvelId]);
        Assert.NotNull((await StateAsync(stokvelId)).Cycles[0].ClosedAtUtc);   // closed
    }

    [Fact]
    public async Task Running_twice_gives_the_same_state_as_running_once()
    {
        var u = await UsersAsync(2);
        var (stokvelId, _) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member)], 1, 500m, new DateOnly(2026, 3, 25));
        var (job, _) = MakeJob(Sast(2026, 3, 26, 8, 0));

        await job.RunOnceAsync([stokvelId]);
        var once = await StateAsync(stokvelId);
        var second = await job.RunOnceAsync([stokvelId]);
        var twice = await StateAsync(stokvelId);

        Assert.Equal(0, second.CyclesClosed);
        Assert.Equal(once.Cycles.Count, twice.Cycles.Count);
        Assert.Equal(once.Arrears.Count, twice.Arrears.Count);
        Assert.Equal(once.Cycles.Select(c => c.ClosedAtUtc), twice.Cycles.Select(c => c.ClosedAtUtc));
    }

    [Fact]
    public async Task Two_runs_at_the_same_time_close_the_cycle_once_and_record_each_arrear_once()
    {
        var u = await UsersAsync(3);
        var (stokvelId, _) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member), (u[2], StokvelRole.Member)], 1, 500m, new DateOnly(2026, 3, 25));
        var (job, _) = MakeJob(Sast(2026, 3, 26, 8, 0));

        // each RunOnceAsync creates its own scope + DbContext inside, like two replicas would
        var results = await Task.WhenAll(job.RunOnceAsync([stokvelId]), job.RunOnceAsync([stokvelId]));

        Assert.Equal(1, results.Sum(r => r.CyclesClosed));
        var (all, arrears) = await StateAsync(stokvelId);
        Assert.Equal(2, all.Count);                     // original + exactly one next cycle
        Assert.Equal(3, arrears.Count);                 // one per member
        Assert.Equal(3, arrears.Select(a => a.UserId).Distinct().Count());
    }

    [Fact]
    public async Task Stokvel_three_cycles_behind_closes_all_missed_cycles_and_opens_the_current_one()
    {
        var u = await UsersAsync(2);
        var (stokvelId, _) = await TestSeed.StokvelAsync(factory.Services,
            [(u[0], StokvelRole.Member), (u[1], StokvelRole.Member)], 1, 500m, new DateOnly(2026, 1, 25));
        var (job, _) = MakeJob(Sast(2026, 4, 10, 9, 0));   // Jan 25, Feb 25 and Mar 25 are all over

        await job.RunOnceAsync([stokvelId]);
        var first = await StateAsync(stokvelId);

        Assert.Equal(4, first.Cycles.Count);
        Assert.All(first.Cycles.Take(3), c => Assert.NotNull(c.ClosedAtUtc));   // 3 missed cycles closed
        Assert.Null(first.Cycles[3].ClosedAtUtc);                                // current one is open
        Assert.Equal(new DateOnly(2026, 4, 25), first.Cycles[3].EndsOn);
        Assert.Equal(6, first.Arrears.Count);                                    // 2 members x 3 missed cycles

        var again = await job.RunOnceAsync([stokvelId]);                         // second run changes nothing
        var second = await StateAsync(stokvelId);
        Assert.Equal(0, again.CyclesClosed);
        Assert.Equal(4, second.Cycles.Count);
        Assert.Equal(6, second.Arrears.Count);
    }

    [Fact]
    public async Task One_failing_stokvel_does_not_stop_another_and_the_job_does_not_throw()
    {
        var uA = await UsersAsync(2);
        var uB = await UsersAsync(2);
        var endsOn = new DateOnly(2026, 3, 25);
        var (bad, badCycles) = await TestSeed.StokvelAsync(factory.Services,
            [(uA[0], StokvelRole.Member), (uA[1], StokvelRole.Member)], 1, 500m, endsOn);
        var (good, _) = await TestSeed.StokvelAsync(factory.Services,
            [(uB[0], StokvelRole.Member), (uB[1], StokvelRole.Member)], 1, 500m, endsOn);

        // impossible state: an arrear already exists for an OPEN cycle -> the unique index makes the close fail
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Arrears.Add(new Arrear(bad, badCycles[0], uA[0], 100, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var (job, _) = MakeJob(Sast(2026, 3, 26, 8, 0));
        var result = await job.RunOnceAsync([bad, good]);   // must not throw

        Assert.Equal(1, result.StokvelsFailed);
        Assert.NotNull((await StateAsync(good)).Cycles[0].ClosedAtUtc);      // the healthy one closed
        var badState = await StateAsync(bad);
        Assert.Null(badState.Cycles[0].ClosedAtUtc);                         // the failed one is untouched (rolled back)
        Assert.Single(badState.Cycles);
        Assert.Single(badState.Arrears);                                     // only the pre-inserted one
    }
}
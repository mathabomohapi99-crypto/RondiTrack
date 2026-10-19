using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Telemetry;

namespace RondiTrack.Jobs;

// 6.4: all settings come from config "Jobs:CycleClose"
public sealed class CycleCloseOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 900;                  // how often the job wakes up
    public int MaxCatchUpCycles { get; set; } = 36;                  // safety cap per stokvel per run
    public string TimeZoneId { get; set; } = "Africa/Johannesburg";  // the stokvel calendar
}

public sealed record CycleCloseResult(int CyclesClosed, int ArrearsRecorded, int StokvelsFailed);

// 6.4: closes finished cycles, records arrears, opens the next cycle. Safe to run twice / on 2 replicas / after a crash.
public sealed class CycleCloseJob(
    IServiceScopeFactory scopes,
    TimeProvider time,                       // 6.4: "now" is injected. Never DateTime.Now
    IOptions<CycleCloseOptions> options,
    ILogger<CycleCloseJob> logger) : BackgroundService
{
    // 6.4: fails at startup if the zone id is wrong (tzdata missing), not at 02:00
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled) { logger.LogInformation("Cycle-close job is disabled"); return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, o.IntervalSeconds)), time);
        await SafeRunAsync(stoppingToken);                           // run once at startup (catches up after downtime)
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await SafeRunAsync(stoppingToken); }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    // 6.4: a failed run never takes the API down. The next tick simply tries again.
    private async Task SafeRunAsync(CancellationToken ct)
    {
        try { await RunOnceAsync(null, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Cycle-close run failed; will retry on the next tick"); }
    }

    // 6.4: tests call this directly. onlyStokvelIds lets a test touch only its own data.
    public async Task<CycleCloseResult> RunOnceAsync(IReadOnlyCollection<Guid>? onlyStokvelIds = null, CancellationToken ct = default)
    {
        using var run = RondiTelemetry.Source.StartActivity("cycles.close.run");   // 6.4: own trace span

        var nowUtc = time.GetUtcNow();
        // 6.4: "which day is it" = the day in SOUTH AFRICA. At 22:00 UTC the SAST date has already changed.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, _zone).DateTime);

        List<Guid> due;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var query = db.ContributionCycles.AsNoTracking().Where(c => c.ClosedAtUtc == null && c.EndsOn < today);
            if (onlyStokvelIds is not null)
            {
                var ids = onlyStokvelIds.ToArray();
                query = query.Where(c => ids.Contains(c.StokvelId));
            }
            due = await query.Select(c => c.StokvelId).Distinct().ToListAsync(ct);
        }

        int closed = 0, arrears = 0, failed = 0;
        foreach (var stokvelId in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (c, a) = await CloseStokvelAsync(stokvelId, nowUtc, today, ct);
                closed += c; arrears += a;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // 6.4: one stokvel's failure never stops the others. Log the stokvel id only (no names/phones/ID numbers: 6.2 rules).
                failed++;
                logger.LogError(ex, "Cycle close failed for stokvel {StokvelId}", stokvelId);
            }
        }

        // 6.4: metrics
        if (closed > 0) RondiMetrics.CyclesClosed(closed);
        if (arrears > 0) RondiMetrics.ArrearsRecorded(arrears);
        run?.SetTag("cycles.closed", closed);
        run?.SetTag("arrears.recorded", arrears);
        run?.SetTag("stokvels.failed", failed);
        logger.LogInformation("Cycle-close run finished: {Closed} closed, {Arrears} arrears, {Failed} stokvels failed", closed, arrears, failed);
        return new CycleCloseResult(closed, arrears, failed);
    }

    // 6.4: ONE transaction per stokvel. Crash halfway = nothing changed. Catch-up (several cycles behind) is in the same transaction.
    private async Task<(int Closed, int Arrears)> CloseStokvelAsync(Guid stokvelId, DateTimeOffset nowUtc, DateOnly today, CancellationToken ct)
    {
        using var span = RondiTelemetry.Source.StartActivity("cycles.close.stokvel");
        span?.SetTag("stokvel.id", stokvelId.ToString());

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var max = Math.Max(1, options.Value.MaxCatchUpCycles);
        var closedAt = nowUtc.UtcDateTime;

        // retry-on-failure is on, so a manual transaction must run inside the execution strategy (same as PayoutService)
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var stokvel = await db.Stokvels.AsNoTracking().Where(s => s.Id == stokvelId)
                .Select(s => new { s.ContributionAmount, s.Frequency }).FirstOrDefaultAsync(ct);
            if (stokvel is null) return (0, 0);

            var dueCents = (long)Math.Round(stokvel.ContributionAmount * 100m);
            var memberIds = await db.StokvelMembers.AsNoTracking().Where(m => m.StokvelId == stokvelId)
                .Select(m => m.UserId).ToListAsync(ct);

            var current = await db.ContributionCycles.AsNoTracking()
                .Where(c => c.StokvelId == stokvelId && c.ClosedAtUtc == null && c.EndsOn < today)
                .OrderBy(c => c.CycleNumber).FirstOrDefaultAsync(ct);

            int cyclesClosed = 0, arrearsRecorded = 0;
            while (current is not null && cyclesClosed < max)
            {
                var cur = current;

                // 6.4: EXACTLY ONCE, enforced by the database. Only one runner can flip ClosedAtUtc from NULL.
                // A second runner waits for the first to commit, then matches 0 rows and does nothing.
                var won = await db.ContributionCycles
                    .Where(c => c.Id == cur.Id && c.ClosedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)closedAt), ct);
                if (won == 0) break;   // someone else already closed it

                // what each member paid in this cycle, in cents
                var paid = await db.Contributions.AsNoTracking().Where(c => c.CycleId == cur.Id)
                    .GroupBy(c => c.UserId).Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
                var paidByUser = paid.ToDictionary(p => p.UserId, p => (long)Math.Round(p.Total * 100m));

                foreach (var userId in memberIds)
                {
                    paidByUser.TryGetValue(userId, out var paidCents);
                    var shortfall = dueCents - paidCents;
                    if (shortfall > 0)   // paid in full = no arrear
                    {
                        db.Arrears.Add(new Arrear(stokvelId, cur.Id, userId, shortfall, closedAt));
                        arrearsRecorded++;
                    }
                }

                // open the next cycle (end date = one period later, still a SAST calendar date)
                var next = new ContributionCycle(stokvelId, cur.CycleNumber + 1, cur.TargetAmount,
                    Advance(cur.EndsOn!.Value, stokvel.Frequency), closedAt);
                db.ContributionCycles.Add(next);

                await db.SaveChangesAsync(ct);
                cyclesClosed++;

                // CATCH-UP: if the new cycle is ALSO already over (server was down), close it too, in order
                current = next.EndsOn < today ? next : null;
            }

            await tx.CommitAsync(ct);   // 0 closed = nothing to commit (lost the race)
            return (cyclesClosed, arrearsRecorded);
        });

        span?.SetTag("cycles.closed", result.Item1);
        return result;
    }

    // 6.4: one period after a date, by the stokvel's frequency
    internal static DateOnly Advance(DateOnly d, ContributionFrequency f) => f switch
    {
        ContributionFrequency.Weekly => d.AddDays(7),
        ContributionFrequency.Fortnightly => d.AddDays(14),
        _ => d.AddMonths(1)
    };
}
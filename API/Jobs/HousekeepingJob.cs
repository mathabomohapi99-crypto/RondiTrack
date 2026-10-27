using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace RondiTrack.Jobs;

// 6.4: N and M come from config "Jobs:Housekeeping" (never hard-coded)
public sealed class HousekeepingOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public int RefreshTokenRetentionDays { get; set; } = 30;     // N: keep tokens this long AFTER they expire
    public int IdempotencyRetentionHours { get; set; } = 168;    // M: keep idempotency records this long (7 days)
}

public sealed record HousekeepingResult(int RefreshTokensDeleted, int IdempotencyEntriesDeleted);

// 6.4: deletes old rows. Deleting rows that are already gone does nothing, so it is safe to run twice.
public sealed class HousekeepingJob(
    IServiceScopeFactory scopes, TimeProvider time, IOptions<HousekeepingOptions> options,
    ILogger<HousekeepingJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (!o.Enabled) { logger.LogInformation("Housekeeping job is disabled"); return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, o.IntervalMinutes)), time);
        await SafeRunAsync(stoppingToken);
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) await SafeRunAsync(stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task SafeRunAsync(CancellationToken ct)
    {
        try { await RunOnceAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Housekeeping run failed; will retry on the next tick"); }   // never takes the API down
    }

    public async Task<HousekeepingResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var activity = RondiTrack.Telemetry.RondiTelemetry.Source.StartActivity("housekeeping.run");
        var o = options.Value;
        var now = time.GetUtcNow().UtcDateTime;   // 6.4: TimeProvider, not DateTime.UtcNow

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        // 6.4: set-based deletes (ExecuteDeleteAsync): nothing is loaded into memory
        var tokenCutoff = now.AddDays(-Math.Max(1, o.RefreshTokenRetentionDays));
        var tokens = await db.RefreshTokens.Where(t => t.ExpiresAtUtc < tokenCutoff).ExecuteDeleteAsync(ct);

        var idemCutoff = now.AddHours(-Math.Max(1, o.IdempotencyRetentionHours));
        var idem = await db.IdempotencyEntries.Where(e => e.CreatedAtUtc < idemCutoff).ExecuteDeleteAsync(ct);

        logger.LogInformation("Housekeeping deleted {Tokens} refresh tokens and {Idem} idempotency records", tokens, idem);
        return new HousekeepingResult(tokens, idem);
    }
}
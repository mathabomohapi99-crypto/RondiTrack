using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Jobs;
using RondiTrack.Telemetry;

namespace RondiTrack.Services;

public interface ICycleCloseService
{
    Task<ClosePreviewResponse> PreviewAsync(Guid stokvelId, Guid cycleId, CancellationToken ct);
    Task<(CloseCycleResponse Response, bool Replayed)> CloseAsync(
        Guid stokvelId, Guid cycleId, string version, string idempotencyKey, CancellationToken ct);
}

// NEW 9.1: the close preview and the safe close.
public sealed class CycleCloseService(RondiTrackDbContext db, TimeProvider time) : ICycleCloseService
{
    private sealed record Snapshot(long DueCents, ContributionFrequency Frequency, List<Guid> MemberIds,
        Dictionary<Guid, long> PaidByUser, Dictionary<Guid, string> Names);

    // Reads who is in the stokvel and what each person paid in this cycle (all in cents)
    private async Task<Snapshot> LoadAsync(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var stokvel = await db.Stokvels.AsNoTracking().Where(s => s.Id == stokvelId)
            .Select(s => new { s.ContributionAmount, s.Frequency }).FirstOrDefaultAsync(ct)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var members = await db.StokvelMembers.AsNoTracking().Where(m => m.StokvelId == stokvelId)
            .OrderBy(m => m.RotationPosition).Select(m => new { m.UserId, m.User.FullName }).ToListAsync(ct);

        var paid = await db.Contributions.AsNoTracking().Where(c => c.CycleId == cycleId)
            .GroupBy(c => c.UserId).Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) }).ToListAsync(ct);

        return new Snapshot(
            Money.ToCents(stokvel.ContributionAmount), stokvel.Frequency,
            members.Select(m => m.UserId).ToList(),
            paid.ToDictionary(p => p.UserId, p => Money.ToCents(p.Total)),
            members.ToDictionary(m => m.UserId, m => m.FullName));
    }

    // The cycle must exist in THIS stokvel and still be open
    private async Task<ContributionCycle> LoadOpenCycleAsync(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await db.ContributionCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cycleId && c.StokvelId == stokvelId, ct)
            ?? throw new DomainNotFoundException($"Cycle {cycleId} was not found.");
        if (cycle.ClosedAtUtc is not null || cycle.Status != CycleStatus.Open)
            throw new CodedDomainException(409, "cycle_closed", "This cycle is already closed.");
        return cycle;
    }

    private static List<ArrearLineResponse> ToResponse(List<ArrearLine> lines, Dictionary<Guid, string> names) =>
        lines.Select(l => new ArrearLineResponse(l.UserId, names.GetValueOrDefault(l.UserId, "Member"), l.ShortfallCents))
             .OrderBy(l => l.MemberName, StringComparer.Ordinal).ToList();

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));   // 64 hex characters

    public async Task<ClosePreviewResponse> PreviewAsync(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var cycle = await LoadOpenCycleAsync(stokvelId, cycleId, ct);
        var snap = await LoadAsync(stokvelId, cycleId, ct);
        var lines = CycleCloseCalculator.Arrears(snap.DueCents, snap.MemberIds, snap.PaidByUser);

        return new ClosePreviewResponse(
            cycle.Id, cycle.CycleNumber,
            CycleCloseCalculator.SaToday(time.GetUtcNow()).ToString("yyyy-MM-dd"),
            snap.DueCents, lines.Sum(l => l.ShortfallCents), ToResponse(lines, snap.Names),
            CycleCloseCalculator.Version(cycleId, snap.DueCents, snap.MemberIds, snap.PaidByUser));
    }

    public async Task<(CloseCycleResponse Response, bool Replayed)> CloseAsync(
        Guid stokvelId, Guid cycleId, string version, string idempotencyKey, CancellationToken ct)
    {
        var hash = Sha($"{cycleId}|{version}");
        var entryKey = $"close:{cycleId}:{idempotencyKey}";   // the key only counts for THIS cycle

        // 1. Same key again? Give back the FIRST answer. Nothing is recorded twice.
        var existing = await db.IdempotencyEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Key == entryKey, ct);
        if (existing is not null) return Replay(existing, hash);

        // 2. The cycle must exist here and still be open (404 or 409)
        var cycle = await LoadOpenCycleAsync(stokvelId, cycleId, ct);

        var closedAt = time.GetUtcNow().UtcDateTime;
        var nextNumber = cycle.CycleNumber + 1;

        // retry-on-failure is on, so a manual transaction must run inside the execution strategy (same as the job)
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync<(CloseCycleResponse Response, bool Replayed)>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // 3. EXACTLY ONCE: only one request can flip ClosedAtUtc from NULL (the database decides)
            var won = await db.ContributionCycles
                .Where(c => c.Id == cycleId && c.StokvelId == stokvelId && c.ClosedAtUtc == null && c.Status == CycleStatus.Open)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)closedAt), ct);
            if (won == 0)
            {
                // someone closed it a moment ago. If it was the same request, give back that result.
                var again = await db.IdempotencyEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Key == entryKey, ct);
                if (again is not null) return Replay(again, hash);
                throw new CodedDomainException(409, "cycle_closed", "This cycle is already closed.");
            }

            // 4. Is what the treasurer SAW still true? If not, undo (the throw rolls the claim back) and say 409.
            var snap = await LoadAsync(stokvelId, cycleId, ct);
            var current = CycleCloseCalculator.Version(cycleId, snap.DueCents, snap.MemberIds, snap.PaidByUser);
            if (!string.Equals(current, version, StringComparison.Ordinal))
                throw new CodedDomainException(409, "stale_preview",
                    "Something changed since you opened the preview. Check the new numbers and confirm again.");

            // 5. Record the arrears (UNIQUE cycle+member in the database is the second safety net)
            var lines = CycleCloseCalculator.Arrears(snap.DueCents, snap.MemberIds, snap.PaidByUser);
            foreach (var l in lines)
                db.Arrears.Add(new Arrear(stokvelId, cycleId, l.UserId, l.ShortfallCents, closedAt));

            // 6. Open the next cycle only if this one had an end date and nobody opened the next one already
            if (cycle.EndsOn is DateOnly ends
                && !await db.ContributionCycles.AnyAsync(c => c.StokvelId == stokvelId && c.CycleNumber == nextNumber, ct))
            {
                db.ContributionCycles.Add(new ContributionCycle(
                    stokvelId, nextNumber, cycle.TargetAmount, CycleCloseJob.Advance(ends, snap.Frequency), closedAt));
            }

            var response = new CloseCycleResponse(
                cycleId, closedAt, lines.Count, lines.Sum(l => l.ShortfallCents), ToResponse(lines, snap.Names));

            // 7. Save the answer under the Idempotency-Key, in the SAME transaction as the arrears
            db.IdempotencyEntries.Add(new IdempotencyEntry(entryKey, hash, JsonSerializer.Serialize(response), closedAt));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            RondiMetrics.CyclesClosed(1);
            if (lines.Count > 0) RondiMetrics.ArrearsRecorded(lines.Count);
            return (response, false);
        });

        return result;
    }

    // Same key + same request = the first answer. Same key + different request = 409.
    private static (CloseCycleResponse Response, bool Replayed) Replay(IdempotencyEntry entry, string hash)
    {
        if (entry.RequestHash != hash)
            throw new DomainConflictException("This Idempotency-Key was already used with a different request.");
        return (JsonSerializer.Deserialize<CloseCycleResponse>(entry.ResponseJson)!, true);
    }
}
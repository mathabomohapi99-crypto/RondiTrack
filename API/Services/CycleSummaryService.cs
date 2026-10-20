using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW 7.2: the small cycle rules that /me/home and /cycles both need
public static class CycleRules
{
    // South Africa is UTC+2 all year (no daylight saving since 1944), so a fixed offset is safe
    private static readonly TimeSpan SaOffset = TimeSpan.FromHours(2);

    // Closed is not stored: a cycle is Closed once the job set ClosedAtUtc. PaidOut wins over Closed.
    public static CycleStatus Display(CycleStatus stored, DateTime? closedAtUtc) =>
        stored == CycleStatus.PaidOut ? CycleStatus.PaidOut
        : closedAtUtc is not null ? CycleStatus.Closed
        : CycleStatus.Open;

    // The due date as a UTC instant. EndsOn is a South African calendar date, so the cycle runs until 23:59:59 SAST that day.
    public static DateTime DueAtUtc(DateOnly? endsOn, DateTime createdAtUtc, ContributionFrequency frequency)
    {
        if (endsOn is DateOnly day)
            return new DateTimeOffset(day.ToDateTime(new TimeOnly(23, 59, 59)), SaOffset).UtcDateTime;
        return Advance(new DateTimeOffset(createdAtUtc, TimeSpan.Zero), frequency).UtcDateTime;
    }

    // one period after a date, by the stokvel's frequency
    public static DateTimeOffset Advance(DateTimeOffset from, ContributionFrequency f) => f switch
    {
        ContributionFrequency.Weekly => from.AddDays(7),
        ContributionFrequency.Fortnightly => from.AddDays(14),
        _ => from.AddMonths(1)
    };
}

public interface ICycleSummaryService
{
    Task<IReadOnlyList<CycleSummaryResponse>> ListAsync(Guid stokvelId, CancellationToken ct);
}

public sealed class CycleSummaryService(RondiTrackDbContext db) : ICycleSummaryService
{
    public async Task<IReadOnlyList<CycleSummaryResponse>> ListAsync(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await db.Stokvels.AsNoTracking().Where(s => s.Id == stokvelId)
            .Select(s => new { s.ContributionAmount, s.Frequency }).FirstOrDefaultAsync(ct)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var members = await db.StokvelMembers.CountAsync(m => m.StokvelId == stokvelId, ct);

        // newest cycle first (highest number)
        var cycles = await db.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderByDescending(c => c.CycleNumber)
            .Select(c => new { c.Id, c.CycleNumber, c.Status, c.CreatedAtUtc, c.EndsOn, c.ClosedAtUtc })
            .ToListAsync(ct);

        // total collected per cycle, summed in the database
        var totals = await db.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .GroupBy(c => c.CycleId)
            .Select(g => new { CycleId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        var collected = totals.ToDictionary(t => t.CycleId, t => Money.ToCents(t.Total));

        // what the whole stokvel owes for one cycle (uses today's member count)
        var dueCents = Money.ToCents(stokvel.ContributionAmount) * members;

        return cycles.Select(c => new CycleSummaryResponse(
            c.Id,
            c.CycleNumber,
            CycleRules.Display(c.Status, c.ClosedAtUtc),
            DateTime.SpecifyKind(c.CreatedAtUtc, DateTimeKind.Utc),
            CycleRules.DueAtUtc(c.EndsOn, c.CreatedAtUtc, stokvel.Frequency),
            collected.GetValueOrDefault(c.Id),
            dueCents)).ToList();
    }
}
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

public interface IHomeService
{
    Task<HomeResponse> GetAsync(Guid userId, CancellationToken ct);
}

public sealed class HomeService(RondiTrackDbContext db, TimeProvider time) : IHomeService
{
    public async Task<HomeResponse> GetAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();   // never DateTime.Now: tests can fake it

        // 1. every stokvel this user is in, with THEIR role in each
        var mine = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.StokvelId, m.Role, m.Stokvel.Name, m.Stokvel.ContributionAmount, m.Stokvel.Frequency })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        if (mine.Count == 0) return new HomeResponse([]);   // empty list, never 404

        var ids = mine.Select(x => x.StokvelId).ToList();

        // 2. the open cycles. CHANGED 7.2: a cycle the job already closed is not "current"
        var openCycles = await db.ContributionCycles.AsNoTracking()
            .Where(c => ids.Contains(c.StokvelId) && c.Status == Domain.CycleStatus.Open && c.ClosedAtUtc == null)
            .OrderByDescending(c => c.CycleNumber)
            .Select(c => new { c.Id, c.StokvelId, c.CycleNumber, c.CreatedAtUtc, c.EndsOn })
            .ToListAsync(ct);

        var cycleIds = openCycles.Select(c => c.Id).ToList();
        // only the caller's own contributions: other members' amounts never leave the server
        var paid = await db.Contributions.AsNoTracking()
            .Where(c => c.UserId == userId && cycleIds.Contains(c.CycleId))
            .Select(c => new { c.CycleId, c.Amount })
            .ToListAsync(ct);

        // NEW 7.2: the caller's OWN arrears per stokvel, added up in cents by the database
        var arrears = await db.Arrears.AsNoTracking()
            .Where(a => a.UserId == userId && ids.Contains(a.StokvelId))
            .GroupBy(a => a.StokvelId)
            .Select(g => new { StokvelId = g.Key, Cents = g.Sum(a => a.ShortfallCents) })
            .ToListAsync(ct);
        var arrearsByStokvel = arrears.ToDictionary(a => a.StokvelId, a => a.Cents);

        var items = mine.Select(s =>
        {
            long? arrearsCents = arrearsByStokvel.TryGetValue(s.StokvelId, out var owedBefore) && owedBefore > 0 ? owedBefore : null;

            var cycle = openCycles.FirstOrDefault(c => c.StokvelId == s.StokvelId);   // newest open one
            if (cycle is null)
                return new HomeStokvelDto(s.StokvelId, s.Name, s.Role, null, 0, 0, null, arrearsCents);

            var start = DateTime.SpecifyKind(cycle.CreatedAtUtc, DateTimeKind.Utc);
            var due = CycleRules.DueAtUtc(cycle.EndsOn, cycle.CreatedAtUtc, s.Frequency);   // CHANGED 7.2: the real due date

            // next payout: unchanged rule, always in the future
            var payout = CycleRules.Advance(new DateTimeOffset(start), s.Frequency);
            while (payout <= now) payout = CycleRules.Advance(payout, s.Frequency);

            // money as integer cents
            var paidCents = Money.ToCents(paid.Where(p => p.CycleId == cycle.Id).Sum(p => p.Amount));
            var dueCents = Money.ToCents(s.ContributionAmount);

            return new HomeStokvelDto(
                s.StokvelId, s.Name, s.Role,
                new HomeCycleDto(cycle.Id, cycle.CycleNumber, Domain.CycleStatus.Open, start, due),
                paidCents, Math.Max(0, dueCents - paidCents), payout.UtcDateTime, arrearsCents);
        }).ToList();

        return new HomeResponse(items);
    }
}
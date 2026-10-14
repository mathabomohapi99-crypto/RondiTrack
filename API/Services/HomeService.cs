using Microsoft.EntityFrameworkCore;
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

        // 1. every stokvel this user is in, with THEIR role in each (one query, only the columns we need)
        var mine = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new { m.StokvelId, m.Role, m.Stokvel.Name, m.Stokvel.ContributionAmount, m.Stokvel.Frequency })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        if (mine.Count == 0) return new HomeResponse([]);   // empty list, never 404

        var ids = mine.Select(x => x.StokvelId).ToList();

        // 2. the open cycles of those stokvels (newest first), then what THIS user paid in them
        var openCycles = await db.ContributionCycles.AsNoTracking()
            .Where(c => ids.Contains(c.StokvelId) && c.Status == Domain.CycleStatus.Open)
            .OrderByDescending(c => c.CycleNumber)
            .Select(c => new { c.Id, c.StokvelId, c.CreatedAtUtc })
            .ToListAsync(ct);

        var cycleIds = openCycles.Select(c => c.Id).ToList();
        // only the caller's own contributions: other members' amounts never leave the server
        var paid = await db.Contributions.AsNoTracking()
            .Where(c => c.UserId == userId && cycleIds.Contains(c.CycleId))
            .Select(c => new { c.CycleId, c.Amount })
            .ToListAsync(ct);

        var items = mine.Select(s =>
        {
            var cycle = openCycles.FirstOrDefault(c => c.StokvelId == s.StokvelId);   // newest open one
            if (cycle is null)
                return new HomeStokvelDto(s.StokvelId, s.Name, s.Role, null, 0, 0, null);

            var start = new DateTimeOffset(cycle.CreatedAtUtc, TimeSpan.Zero);
            var end = Advance(start, s.Frequency);
            while (end <= now) end = Advance(end, s.Frequency);   // payout date is always in the future

            // money as integer cents: a JavaScript number holds them exactly (no 0.1 + 0.2 problems)
            var paidCents = (long)Math.Round(paid.Where(p => p.CycleId == cycle.Id).Sum(p => p.Amount) * 100m);
            var dueCents = (long)Math.Round(s.ContributionAmount * 100m);

            return new HomeStokvelDto(
                s.StokvelId, s.Name, s.Role,
                new HomeCycleDto(cycle.Id, Domain.CycleStatus.Open, start, end),
                paidCents, Math.Max(0, dueCents - paidCents), end);
        }).ToList();

        return new HomeResponse(items);
    }

    // one period after a date, by the stokvel's frequency
    private static DateTimeOffset Advance(DateTimeOffset from, Domain.ContributionFrequency f) => f switch
    {
        Domain.ContributionFrequency.Weekly => from.AddDays(7),
        Domain.ContributionFrequency.Fortnightly => from.AddDays(14),
        _ => from.AddMonths(1)
    };
}
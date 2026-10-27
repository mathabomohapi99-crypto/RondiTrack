using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW 8.1: builds "my stokvels" and the stokvel detail. A fixed number of queries, never one per stokvel.
public sealed class MyStokvelsService(RondiTrackDbContext db)
{
    // Every stokvel the user belongs to, with their role in each (empty list if none)
    public async Task<IReadOnlyList<MyStokvelResponse>> ListMineAsync(Guid userId, CancellationToken ct)
    {
        // query 1: my memberships with the stokvel's basic facts
        var memberships = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Stokvel.Name)
            .Select(m => new
            {
                m.StokvelId,
                m.Stokvel.Name,
                m.Stokvel.ContributionAmount,
                m.Stokvel.Frequency,
                m.Role,
                MemberCount = m.Stokvel.Memberships.Count
            })
            .ToListAsync(ct);

        if (memberships.Count == 0) return [];   // no stokvels is still a normal 200 with an empty list

        // queries 2 and 3: current cycle + sums for all those stokvels at once
        var frequencies = memberships.ToDictionary(m => m.StokvelId, m => m.Frequency);
        var cycles = await LoadCurrentCyclesAsync(frequencies, userId, ct);

        return memberships.Select(m =>
        {
            cycles.TryGetValue(m.StokvelId, out var found);
            var summary = cycles.ContainsKey(m.StokvelId) ? found.Summary : null;
            var paid = cycles.ContainsKey(m.StokvelId) ? found.MyPaid : 0m;
            var owed = summary is null ? 0m : Math.Max(m.ContributionAmount - paid, 0m);   // what I still owe this cycle

            return new MyStokvelResponse(
                m.StokvelId, m.Name, m.ContributionAmount, m.Frequency, m.MemberCount, m.Role,
                paid, owed, summary?.NextPayoutDateUtc, summary);
        }).ToList();
    }

    // Detail of ONE stokvel (the controller already checked the caller may see it)
    public async Task<StokvelDetailResponse> GetDetailAsync(Stokvel stokvel, Guid? userId, CancellationToken ct)
    {
        StokvelRole? role = null;
        if (userId is Guid uid)
        {
            role = await db.StokvelMembers.AsNoTracking()
                .Where(m => m.UserId == uid && m.StokvelId == stokvel.Id)
                .Select(m => (StokvelRole?)m.Role)
                .FirstOrDefaultAsync(ct);
        }

        var cycles = await LoadCurrentCyclesAsync(
            new Dictionary<Guid, ContributionFrequency> { [stokvel.Id] = stokvel.Frequency }, userId, ct);
        var summary = cycles.TryGetValue(stokvel.Id, out var found) ? found.Summary : null;

        return new StokvelDetailResponse(
            stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.Frequency, stokvel.MaxMembers,
            stokvel.Members.Count, stokvel.IsFull, stokvel.PayoutPerCycle, stokvel.CreatedAtUtc,
            role, summary);
    }

    // For each stokvel: its newest OPEN cycle, how much was collected, and how much THIS user paid
    private async Task<Dictionary<Guid, (CycleSummaryResponse Summary, decimal MyPaid)>> LoadCurrentCyclesAsync(
        Dictionary<Guid, ContributionFrequency> frequencies, Guid? userId, CancellationToken ct)
    {
        var stokvelIds = frequencies.Keys.ToList();

        var open = await db.ContributionCycles.AsNoTracking()
            .Where(c => stokvelIds.Contains(c.StokvelId) && c.Status == CycleStatus.Open)
            .Select(c => new { c.Id, c.StokvelId, c.CycleNumber, c.TargetAmount, c.CreatedAtUtc })
            .ToListAsync(ct);

        // newest open cycle per stokvel
        var latest = open.GroupBy(c => c.StokvelId)
            .Select(g => g.OrderByDescending(c => c.CycleNumber).First())
            .ToList();

        var result = new Dictionary<Guid, (CycleSummaryResponse, decimal)>();
        if (latest.Count == 0) return result;

        var cycleIds = latest.Select(c => c.Id).ToList();
        var me = userId ?? Guid.Empty;

        // one grouped query: total collected and my share, per cycle (runs in PostgreSQL)
        var sums = (await db.Contributions.AsNoTracking()
            .Where(c => cycleIds.Contains(c.CycleId))
            .GroupBy(c => c.CycleId)
            .Select(g => new
            {
                CycleId = g.Key,
                Total = g.Sum(x => x.Amount),
                Mine = g.Sum(x => x.UserId == me ? x.Amount : 0m)
            })
            .ToListAsync(ct)).ToDictionary(s => s.CycleId);

        foreach (var c in latest)
        {
            sums.TryGetValue(c.Id, out var s);
            var collected = s?.Total ?? 0m;
            var mine = s?.Mine ?? 0m;
            var next = NextPayout(c.CreatedAtUtc, frequencies[c.StokvelId]);
            result[c.StokvelId] = (
                new CycleSummaryResponse(c.Id, c.CycleNumber, c.TargetAmount, collected, c.CreatedAtUtc, next),
                mine);
        }

        return result;
    }

    // The model has no payout schedule, so the next payout is the cycle start plus one period
    private static DateTime NextPayout(DateTime start, ContributionFrequency frequency) => frequency switch
    {
        ContributionFrequency.Weekly => start.AddDays(7),
        ContributionFrequency.Fortnightly => start.AddDays(14),
        _ => start.AddMonths(1)
    };
}
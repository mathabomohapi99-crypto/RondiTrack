using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Jobs;

// NEW 9.2: test-only data for the end-to-end suite. Runs ONLY with --seed-e2e (the compose "seed" service), never on startup.
// Roles cannot be set through the API (6.1 gap), so the people and their roles are seeded here. Tests make everything else.
public static class E2eSeed
{
    public static readonly Guid Sipho = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid Thandi = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    public static readonly Guid Kagiso = Guid.Parse("b0000000-0000-0000-0000-000000000003");
    public static readonly Guid Family = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid Work = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid Perf = Guid.Parse("a0000000-0000-0000-0000-000000000003");

    public static async Task RunAsync(RondiTrackDbContext db, IPasswordHasher<User> hasher, string password)
    {
        if (await db.Users.AnyAsync(u => u.Id == Sipho)) return;   // already seeded

        var people = new[]
        {
            new User(Sipho, "Sipho Dlamini", "sipho.e2e@example.com"),
            new User(Thandi, "Thandi Nkosi", "thandi.e2e@example.com"),
            new User(Kagiso, "Kagiso Molefe", "kagiso.e2e@example.com"),
        };
        foreach (var u in people) u.SetPasswordHash(hasher.HashPassword(u, password));

        var family = new Stokvel(Family, "E2E Family Stokvel", 500m, ContributionFrequency.Monthly, 6);
        var work = new Stokvel(Work, "E2E Work Stokvel", 250m, ContributionFrequency.Monthly, 6);
        var perf = new Stokvel(Perf, "E2E Perf Stokvel", 500m, ContributionFrequency.Monthly, 50);
        var fillers = Enumerable.Range(1, 39).Select(i => new User($"Perf Member {i}", $"perf{i}@e2e.example.com")).ToList();

        db.Users.AddRange(people);
        db.Users.AddRange(fillers);
        db.Stokvels.AddRange(family, work, perf);
        await db.SaveChangesAsync();   // parents first

        // Sipho: treasurer of Family, ordinary member of Work. Kagiso: treasurer of Work. Thandi: member of both.
        db.StokvelMembers.AddRange(
            new StokvelMember(Family, Sipho, 1, StokvelRole.Treasurer),
            new StokvelMember(Family, Thandi, 2),
            new StokvelMember(Work, Kagiso, 1, StokvelRole.Treasurer),
            new StokvelMember(Work, Sipho, 2),
            new StokvelMember(Work, Thandi, 3),
            new StokvelMember(Perf, Sipho, 1, StokvelRole.Treasurer));
        var pos = 2;
        foreach (var f in fillers) db.StokvelMembers.Add(new StokvelMember(Perf, f.Id, pos++));

        // Perf stokvel: 40 members and a year of contributions (11 closed cycles, 1 open)
        var cycles = Enumerable.Range(1, 12).Select(n => new ContributionCycle(Perf, n, 500m)).ToList();
        db.ContributionCycles.AddRange(cycles);
        await db.SaveChangesAsync();

        var ids = new List<Guid> { Sipho };
        ids.AddRange(fillers.Select(f => f.Id));
        var start = DateTime.UtcNow.AddMonths(-12);
        foreach (var cycle in cycles)
        {
            var closed = cycle.CycleNumber < 12;
            var takers = closed ? ids : ids.Take(20).ToList();   // the open cycle is half paid
            foreach (var userId in takers)
            {
                var c = new Contribution(Perf, userId, cycle.Id, 500m);
                db.Contributions.Add(c);
                db.Entry(c).Property(nameof(Contribution.RecordedAtUtc)).CurrentValue = start.AddMonths(cycle.CycleNumber).AddHours(userId.GetHashCode() % 6);
            }
            if (closed)
                db.Entry(cycle).Property(nameof(ContributionCycle.ClosedAtUtc)).CurrentValue = (DateTime?)start.AddMonths(cycle.CycleNumber + 1);
        }
        await db.SaveChangesAsync();
    }
}
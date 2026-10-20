using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RondiTrack.Jobs;

// 6.4 + 7.2: demo accounts and demo data for the frontend developer. Only runs when Demo:Enabled=true.
public static class DemoSeed
{
    public static async Task RunAsync(RondiTrackDbContext db, IPasswordHasher<User> hasher, TimeProvider time, string password)
    {
        // 1) give the SEEDED users (only those) a password, taken from the .env file
        var seededIds = SeedData.Users.Select(u => u.Id).ToArray();
        var users = await db.Users.Where(u => seededIds.Contains(u.Id) && u.PasswordHash == null).ToListAsync();
        foreach (var u in users) u.SetPasswordHash(hasher.HashPassword(u, password));
        await db.SaveChangesAsync();

        var thandi = SeedData.Users[0].Id;
        var sipho = SeedData.Users[1].Id;
        var lerato = SeedData.Users[2].Id;
        var kagiso = SeedData.Users[3].Id;
        var ubuntu = SeedData.Stokvels[0].Id;
        var grocery = SeedData.Stokvels[1].Id;

        // 2) Thandi is Treasurer in Ubuntu Savers and a plain Member in Festive Grocery Club
        await db.StokvelMembers.Where(m => m.UserId == thandi && m.StokvelId == ubuntu)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, StokvelRole.Treasurer));

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        var now = time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));

        // small helper: add a contribution with a chosen status and time
        void Pay(Guid stokvelId, Guid userId, Guid cycleId, decimal amount, ContributionStatus status, DateTime recordedAtUtc)
        {
            var c = new Contribution(stokvelId, userId, cycleId, amount);
            db.Contributions.Add(c);
            db.Entry(c).Property(nameof(Contribution.RecordedAtUtc)).CurrentValue = recordedAtUtc;
            db.Entry(c).Property(nameof(Contribution.Status)).CurrentValue = status;
        }

        // 3) NEW 7.2: Ubuntu Savers gets three cycles: #1 PaidOut, #2 Closed, #3 Open
        if (!await db.ContributionCycles.AnyAsync(c => c.StokvelId == ubuntu))
        {
            var u1 = new ContributionCycle(ubuntu, 1, 500m, today.AddDays(-61), now.AddDays(-91));
            u1.MarkPaidOut();
            var u2 = new ContributionCycle(ubuntu, 2, 500m, today.AddDays(-31), now.AddDays(-61));
            var u3 = new ContributionCycle(ubuntu, 3, 500m, today.AddDays(30), now.AddDays(-1));
            db.ContributionCycles.AddRange(u1, u2, u3);
            await db.SaveChangesAsync();

            // cycles 1 and 2 are over: mark them closed so the cycle-close job leaves them alone
            await db.ContributionCycles.Where(c => c.Id == u1.Id || c.Id == u2.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)now));

            Pay(ubuntu, thandi, u1.Id, 500m, ContributionStatus.Confirmed, now.AddDays(-80));
            Pay(ubuntu, sipho, u1.Id, 500m, ContributionStatus.Confirmed, now.AddDays(-79));
            Pay(ubuntu, lerato, u1.Id, 500m, ContributionStatus.Confirmed, now.AddDays(-78));

            Pay(ubuntu, thandi, u2.Id, 500m, ContributionStatus.Confirmed, now.AddDays(-50));
            Pay(ubuntu, sipho, u2.Id, 500m, ContributionStatus.Paid, now.AddDays(-49));
            Pay(ubuntu, lerato, u2.Id, 0.10m, ContributionStatus.Paid, now.AddDays(-48));   // 10 cents
            db.Arrears.Add(new Arrear(ubuntu, u2.Id, lerato, 49990, now));                  // Lerato was short by R499,90

            Pay(ubuntu, thandi, u3.Id, 500m, ContributionStatus.Confirmed, now.AddDays(-3));            // Thandi is all paid up
            Pay(ubuntu, sipho, u3.Id, 500m, ContributionStatus.Paid,
                new DateTime(2026, 10, 31, 22, 30, 0, DateTimeKind.Utc));                              // the midnight case: 00:30 on 1 Nov in SA
            Pay(ubuntu, lerato, u3.Id, 500m, ContributionStatus.Pending, now.AddDays(-1));
        }

        // 4) NEW 7.2: Festive Grocery Club: Thandi (a plain Member) is in arrears and still owes this cycle
        if (!await db.ContributionCycles.AnyAsync(c => c.StokvelId == grocery))
        {
            var g1 = new ContributionCycle(grocery, 1, 250m, today.AddDays(-30), now.AddDays(-60));
            var g2 = new ContributionCycle(grocery, 2, 250m, today.AddDays(3), now.AddDays(-27));
            db.ContributionCycles.AddRange(g1, g2);
            await db.SaveChangesAsync();

            await db.ContributionCycles.Where(c => c.Id == g1.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)now));

            Pay(grocery, kagiso, g1.Id, 250m, ContributionStatus.Confirmed, now.AddDays(-40));
            db.Arrears.Add(new Arrear(grocery, g1.Id, thandi, 25000, now));                 // Thandi paid nothing in cycle 1
            Pay(grocery, kagiso, g2.Id, 250m, ContributionStatus.Confirmed, now.AddDays(-2));
        }

        await db.SaveChangesAsync();
    }
}
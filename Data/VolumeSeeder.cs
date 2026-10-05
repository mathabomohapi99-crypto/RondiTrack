using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Data;

// NEW 5.3: creates 3 stokvels x 100 members x 60 cycles = 18 000 contributions (100 per cycle).
// It NEVER runs on startup. Only when you start the app with:  dotnet run -- --seed-volume
// Members are added as StokvelMember rows directly (the 50-member cap is a rule inside the Stokvel class,
// the database does not enforce it), so each cycle gets 100 contributions.
public static class VolumeSeeder
{
    private const int StokvelCount = 3;
    private const int MembersPerStokvel = 100;
    private const int CyclesPerStokvel = 60;

    public static async Task RunAsync(RondiTrackDbContext db)
    {
        if (await db.Stokvels.AnyAsync(s => s.Name == "Volume Stokvel 1"))
        {
            Console.WriteLine("Volume data already exists. Nothing to do.");
            return;
        }

        var rnd = new Random(42);   // fixed seed: same data every time
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var s = 1; s <= StokvelCount; s++)
        {
            var stokvel = new Stokvel($"Volume Stokvel {s}", 100m, ContributionFrequency.Monthly, 50);
            var users = Enumerable.Range(1, MembersPerStokvel)
                .Select(i => new User($"Volume User {s}-{i}", $"vol{s}-{i}@volume.example.com"))
                .ToList();

            db.Stokvels.Add(stokvel);
            db.Users.AddRange(users);
            await db.SaveChangesAsync();                         // parents first

            db.StokvelMembers.AddRange(users.Select((u, i) => new StokvelMember(stokvel.Id, u.Id, i + 1)));
            var cycles = Enumerable.Range(1, CyclesPerStokvel)
                .Select(n => new ContributionCycle(stokvel.Id, n, 10_000m))
                .ToList();
            db.ContributionCycles.AddRange(cycles);
            await db.SaveChangesAsync();                         // then members and cycles

            foreach (var cycle in cycles)
                foreach (var user in users)
                {
                    var c = new Contribution(stokvel.Id, user.Id, cycle.Id, rnd.Next(10, 100) * 10m);
                    db.Contributions.Add(c);
                    // RecordedAtUtc has a private setter, so set it through EF. Only 6 distinct hours per cycle,
                    // so many rows share the same date on purpose (that is what makes the tiebreaker matter).
                    db.Entry(c).Property(nameof(Contribution.RecordedAtUtc)).CurrentValue =
                        start.AddDays(cycle.CycleNumber * 30).AddHours(rnd.Next(0, 6));
                }

            await db.SaveChangesAsync();                         // then contributions
            db.ChangeTracker.Clear();
            Console.WriteLine($"Seeded Volume Stokvel {s}.");
        }

        Console.WriteLine("Done: 3 stokvels, 300 users, 180 cycles, 18 000 contributions.");
    }
}
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace RondiTrack.Jobs;

// 6.4: demo accounts for the frontend developer. Only runs when Demo:Enabled=true (never in real production).
public static class DemoSeed
{
    public static async Task RunAsync(RondiTrackDbContext db, IPasswordHasher<User> hasher, TimeProvider time, string password)
    {
        // 1) give the SEEDED users (only those) a password, taken from the .env file
        var seededIds = SeedData.Users.Select(u => u.Id).ToArray();
        var users = await db.Users.Where(u => seededIds.Contains(u.Id) && u.PasswordHash == null).ToListAsync();
        foreach (var u in users) u.SetPasswordHash(hasher.HashPassword(u, password));
        await db.SaveChangesAsync();

        // 2) Thandi (belongs to TWO stokvels) is Treasurer in Ubuntu Savers and a plain Member in Festive Grocery Club
        var thandi = SeedData.Users[0].Id;
        var ubuntu = SeedData.Stokvels[0].Id;
        await db.StokvelMembers.Where(m => m.UserId == thandi && m.StokvelId == ubuntu)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, StokvelRole.Treasurer));

        // 3) one open cycle per demo stokvel, ending in 30 days (SAST date), so /me/home has something to show
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        foreach (var s in SeedData.Stokvels)
            if (!await db.ContributionCycles.AnyAsync(c => c.StokvelId == s.Id))
                db.ContributionCycles.Add(new ContributionCycle(s.Id, 1, s.ContributionAmount, today.AddDays(30), now.UtcDateTime));
        await db.SaveChangesAsync();
    }
}
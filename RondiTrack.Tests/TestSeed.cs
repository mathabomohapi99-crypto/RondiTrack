using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Auth;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Tests;

// 6.3/6.4: shared helpers so each test builds its own users/stokvels (no leftovers between tests)
public static class TestSeed
{
    // Makes a real user row, returns its id
    public static async Task<Guid> UserAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var u = new User("Test User", $"{Guid.NewGuid():N}@test.example");
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    // Makes a stokvel with the given members/roles and N cycles.
    // firstEndsOn = null -> plain cycles (no end date, the job ignores them)
    public static async Task<(Guid StokvelId, Guid[] CycleIds)> StokvelAsync(
        IServiceProvider sp, (Guid UserId, StokvelRole Role)[] members, int cycles = 1,
        decimal amount = 500m, DateOnly? firstEndsOn = null,
        ContributionFrequency freq = ContributionFrequency.Monthly)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();

        var s = new Stokvel($"Test {Guid.NewGuid():N}", amount, freq, Math.Max(2, members.Length));
        db.Stokvels.Add(s);
        await db.SaveChangesAsync();                         // parent first

        for (var i = 0; i < members.Length; i++)
            db.StokvelMembers.Add(new StokvelMember(s.Id, members[i].UserId, i + 1, members[i].Role));

        var ids = new List<Guid>();
        for (var n = 1; n <= cycles; n++)
        {
            var c = firstEndsOn is null
                ? new ContributionCycle(s.Id, n, amount)
                : new ContributionCycle(s.Id, n, amount, firstEndsOn.Value.AddMonths(n - 1), DateTime.UtcNow);
            db.ContributionCycles.Add(c);
            ids.Add(c.Id);
        }
        await db.SaveChangesAsync();
        return (s.Id, ids.ToArray());
    }

    // Mints a real access token with the host's own TokenService (same key, same checks)
    public static string Token(IServiceProvider sp, Guid userId) =>
        sp.GetRequiredService<ITokenService>().CreateAccessToken(userId, false).Value;
}
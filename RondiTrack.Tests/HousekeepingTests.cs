using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Auth;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Jobs;

namespace RondiTrack.Tests;

// 6.4: housekeeping must delete old rows WITHOUT weakening reuse detection or idempotency
public class HousekeepingTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static string RandomHash() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));   // 64 hex chars

    private async Task<Guid> TokenRowAsync(Guid userId, DateTime expiresAtUtc, string? hash = null, Guid? family = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var t = new RefreshToken(userId, family ?? Guid.NewGuid(), hash ?? RandomHash(), DateTime.UtcNow.AddDays(-40), expiresAtUtc);
        db.RefreshTokens.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    [Fact]
    public async Task Token_inside_retention_survives_and_token_past_it_is_deleted()
    {
        var user = await TestSeed.UserAsync(factory.Services);
        var recent = await TokenRowAsync(user, DateTime.UtcNow.AddDays(-1));    // expired yesterday (N = 30 days)
        var old = await TokenRowAsync(user, DateTime.UtcNow.AddDays(-31));      // expired 31 days ago

        var result = await factory.Services.GetRequiredService<HousekeepingJob>().RunOnceAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.True(await db.RefreshTokens.AnyAsync(t => t.Id == recent));
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.Id == old));
        Assert.True(result.RefreshTokensDeleted >= 1);
    }

    [Fact]
    public async Task Reuse_detection_still_works_on_a_token_that_expired_yesterday()
    {
        var user = await TestSeed.UserAsync(factory.Services);
        var family = Guid.NewGuid();
        var (raw, hash) = factory.Services.GetRequiredService<ITokenService>().CreateRefreshToken();

        var spent = await TokenRowAsync(user, DateTime.UtcNow.AddDays(-1), hash, family);     // old, already used, expired yesterday
        var live = await TokenRowAsync(user, DateTime.UtcNow.AddDays(5), null, family);       // the user's current token, same login
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            await db.RefreshTokens.Where(t => t.Id == spent).ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAtUtc, (DateTime?)DateTime.UtcNow.AddDays(-2)));
        }

        await factory.Services.GetRequiredService<HousekeepingJob>().RunOnceAsync();   // must keep the spent token

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.TryAddWithoutValidation("Cookie", $"rondi_refresh={raw}");
        var res = await factory.CreateClient().SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.NotNull((await db2.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == live)).RevokedAtUtc);   // whole session revoked
    }

    [Fact]
    public async Task Idempotency_record_inside_retention_still_replays_and_an_old_one_is_deleted()
    {
        var user = await TestSeed.UserAsync(factory.Services);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(factory.Services, [(user, StokvelRole.Member)]);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestSeed.Token(factory.Services, user));

        HttpRequestMessage Post(string key)
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
            { Content = JsonContent.Create(new { userId = user, cycleId = cycles[0], amount = 100m }) };
            r.Headers.Add("Idempotency-Key", key);
            return r;
        }

        var key = $"hk-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(Post(key))).StatusCode);

        var oldKey = $"old-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.IdempotencyEntries.Add(new IdempotencyEntry(oldKey, RandomHash(), "{}", DateTime.UtcNow.AddHours(-200)));   // M = 168 h
            await db.SaveChangesAsync();
        }

        await factory.Services.GetRequiredService<HousekeepingJob>().RunOnceAsync();

        var replay = await client.SendAsync(Post(key));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());   // still replays

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.False(await db2.IdempotencyEntries.AnyAsync(e => e.Key == oldKey));          // the old one is gone
    }
}
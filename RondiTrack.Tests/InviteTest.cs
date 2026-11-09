using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class InviteTests(PostgresApiFactory factory)
{
    private IServiceProvider Sp => factory.Services;

    private async Task<string> CreateCodeAsync(Guid treasurer, Guid stokvelId)
    {
        var res = await factory.CreateClientFor(treasurer).PostAsync($"/api/v1/stokvels/{stokvelId}/invites", null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    [Fact]
    public async Task Treasurer_can_create_an_invite_a_member_gets_403_and_an_outsider_gets_404()
    {
        var t = await TestSeed.UserAsync(Sp); var m = await TestSeed.UserAsync(Sp); var outsider = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (m, StokvelRole.Member)]);
        var url = $"/api/v1/stokvels/{stokvelId}/invites";

        Assert.Equal(8, (await CreateCodeAsync(t, stokvelId)).Length);
        Assert.Equal(HttpStatusCode.Forbidden, (await factory.CreateClientFor(m).PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClientFor(outsider).PostAsync(url, null)).StatusCode);
    }

    [Fact]
    public async Task Preview_shows_only_what_a_new_person_needs()
    {
        var t = await TestSeed.UserAsync(Sp); var m = await TestSeed.UserAsync(Sp); var outsider = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (m, StokvelRole.Member)], amount: 500m);
        await W9.MakeRoomAsync(stokvelId);
        var code = await CreateCodeAsync(t, stokvelId);

        var res = await factory.CreateClientFor(outsider).GetAsync($"/api/v1/invites/{code}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();

        // nothing about the members was SENT (the seeded members are all called "Test User")
        Assert.DoesNotContain("Test User", raw);
        Assert.DoesNotContain(m.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(t.ToString(), raw, StringComparison.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(raw);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "alreadyMember", "contributionAmountCents", "expiresAtUtc", "frequency", "maxMembers", "memberCount", "stokvelId", "stokvelName" }.Order().ToArray(), names);
        Assert.Equal(50000, doc.RootElement.GetProperty("contributionAmountCents").GetInt64());
        Assert.False(doc.RootElement.GetProperty("alreadyMember").GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("stokvelId").ValueKind);   // a non-member never learns the id

        // an existing member gets alreadyMember + the id (to link to the stokvel)
        var mine = await (await factory.CreateClientFor(m).GetAsync($"/api/v1/invites/{code}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(mine.GetProperty("alreadyMember").GetBoolean());
        Assert.Equal(stokvelId, mine.GetProperty("stokvelId").GetGuid());
    }

    [Fact]
    public async Task Unknown_code_is_404_and_expired_code_is_410()
    {
        var t = await TestSeed.UserAsync(Sp); var someone = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (someone, StokvelRole.Member)]);
        var client = factory.CreateClientFor(someone);

        var unknown = await client.GetAsync("/api/v1/invites/ZZZZZZZZ");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("invite_not_found", await CodeOf(unknown));

        var oldCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using (var db = TestDb.NewDb())
        {
            db.Invites.Add(new Invite(stokvelId, t, oldCode, DateTime.UtcNow.AddDays(-10), TimeSpan.FromDays(7)));
            await db.SaveChangesAsync();
        }
        var expired = await client.GetAsync($"/api/v1/invites/{oldCode}");
        Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
        Assert.Equal("invite_expired", await CodeOf(expired));
        Assert.Equal(HttpStatusCode.Gone, (await client.PostAsync($"/api/v1/invites/{oldCode}/join", null)).StatusCode);
    }

    [Fact]
    public async Task Join_works_once_and_the_second_join_says_already_a_member()
    {
        var t = await TestSeed.UserAsync(Sp); var newcomer = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (await TestSeed.UserAsync(Sp), StokvelRole.Member)]);
        await W9.MakeRoomAsync(stokvelId);
        var code = await CreateCodeAsync(t, stokvelId);
        var client = factory.CreateClientFor(newcomer);

        var first = await client.PostAsync($"/api/v1/invites/{code}/join", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(stokvelId, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("stokvelId").GetGuid());

        var second = await client.PostAsync($"/api/v1/invites/{code}/join", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("already_member", await CodeOf(second));

        await using var db = TestDb.NewDb();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(x => x.StokvelId == stokvelId && x.UserId == newcomer));
    }

    [Fact]
    public async Task Two_joins_at_the_same_moment_make_one_membership()
    {
        var t = await TestSeed.UserAsync(Sp); var newcomer = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (await TestSeed.UserAsync(Sp), StokvelRole.Member)]);
        await W9.MakeRoomAsync(stokvelId);
        var code = await CreateCodeAsync(t, stokvelId);
        var client = factory.CreateClientFor(newcomer);

        var both = await Task.WhenAll(client.PostAsync($"/api/v1/invites/{code}/join", null), client.PostAsync($"/api/v1/invites/{code}/join", null));

        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, both.Select(r => r.StatusCode).Order().ToArray());
        await using var db = TestDb.NewDb();
        Assert.Equal(1, await db.StokvelMembers.CountAsync(x => x.StokvelId == stokvelId && x.UserId == newcomer));
    }

    [Fact]
    public async Task Joining_a_full_stokvel_says_stokvel_full()
    {
        var t = await TestSeed.UserAsync(Sp); var newcomer = await TestSeed.UserAsync(Sp);
        var (stokvelId, _) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (await TestSeed.UserAsync(Sp), StokvelRole.Member)]);   // max 2, already 2
        var code = await CreateCodeAsync(t, stokvelId);

        var res = await factory.CreateClientFor(newcomer).PostAsync($"/api/v1/invites/{code}/join", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("stokvel_full", await CodeOf(res));
    }
}
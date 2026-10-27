using System.Net;
using System.Text.Json;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 8.1: tests for GET /api/me, GET /api/me/stokvels and the new fields on GET /api/stokvels/{id}
[Collection("Postgres collection")]
public class MyStokvelsTests(PostgresApiFactory factory)
{
    // ---- helpers ----

    private static async Task<(Guid Id, string Email)> CreateUserAsync()
    {
        await using var db = TestDb.NewDb();
        var email = $"me-{Guid.NewGuid():N}@example.com";
        var user = new User("Me Tester", email);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, email);
    }

    // a stokvel (contribution R100) with one open cycle and the given members
    private static async Task<(Guid StokvelId, Guid CycleId)> SeedStokvelAsync(
        string name, params (Guid UserId, StokvelRole Role)[] members)
    {
        await using var db = TestDb.NewDb();

        var stokvel = new Stokvel(name, 100m, ContributionFrequency.Monthly, 5);
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();   // parent first

        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        var position = 1;
        foreach (var (userId, role) in members)
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, userId, position++, role));
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();

        return (stokvel.Id, cycle.Id);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    // ---- tests ----

    [Fact]
    public async Task Me_Returns_The_Signed_In_Users_Details()
    {
        var (userId, email) = await CreateUserAsync();

        var response = await factory.CreateClientFor(userId).GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await ReadAsync(response);
        Assert.Equal(userId, doc.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(email, doc.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_Without_A_Token_Returns_401()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task My_Stokvels_Is_200_With_An_Empty_List_For_A_User_In_No_Stokvels()
    {
        var (userId, _) = await CreateUserAsync();

        var response = await factory.CreateClientFor(userId).GetAsync("/api/me/stokvels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);   // NOT 404, NOT 403
        using var doc = await ReadAsync(response);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task My_Stokvels_Returns_My_Role_In_Each_Stokvel_And_Nothing_Else()
    {
        var (me, _) = await CreateUserAsync();
        var (other, _) = await CreateUserAsync();
        var nameA = $"Family {Guid.NewGuid():N}";
        var nameB = $"Work {Guid.NewGuid():N}";
        await SeedStokvelAsync(nameA, (me, StokvelRole.Treasurer));                                // Treasurer here
        await SeedStokvelAsync(nameB, (other, StokvelRole.Treasurer), (me, StokvelRole.Member));   // Member here
        await SeedStokvelAsync($"Not Mine {Guid.NewGuid():N}", (other, StokvelRole.Member));       // not mine

        var response = await factory.CreateClientFor(me).GetAsync("/api/me/stokvels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await ReadAsync(response);
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);   // only my two
        Assert.Equal("Treasurer", items.Single(i => i.GetProperty("name").GetString() == nameA).GetProperty("myRole").GetString());
        Assert.Equal("Member", items.Single(i => i.GetProperty("name").GetString() == nameB).GetProperty("myRole").GetString());
    }

    [Fact]
    public async Task My_Stokvels_Shows_Paid_And_Owed_For_The_Open_Cycle()
    {
        var (me, _) = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync($"Paid {Guid.NewGuid():N}", (me, StokvelRole.Member));

        await using (var db = TestDb.NewDb())
        {
            db.Contributions.Add(new Contribution(stokvelId, me, cycleId, 40m));   // I paid R40 of R100
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClientFor(me).GetAsync("/api/me/stokvels");

        using var doc = await ReadAsync(response);
        var item = doc.RootElement.EnumerateArray().Single();
        Assert.Equal(40m, item.GetProperty("paidThisCycle").GetDecimal());
        Assert.Equal(60m, item.GetProperty("owedThisCycle").GetDecimal());
        Assert.Equal(JsonValueKind.String, item.GetProperty("nextPayoutDateUtc").ValueKind);
    }

    [Fact]
    public async Task Stokvel_Detail_Includes_My_Role_And_The_Current_Cycle_Summary()
    {
        var (me, _) = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync($"Detail {Guid.NewGuid():N}", (me, StokvelRole.Treasurer));

        await using (var db = TestDb.NewDb())
        {
            db.Contributions.Add(new Contribution(stokvelId, me, cycleId, 100m));
            await db.SaveChangesAsync();
        }

        var response = await factory.CreateClientFor(me).GetAsync($"/api/stokvels/{stokvelId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await ReadAsync(response);
        Assert.Equal("Treasurer", doc.RootElement.GetProperty("myRole").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("memberCount").GetInt32());
        var cycle = doc.RootElement.GetProperty("currentCycle");
        Assert.Equal(1, cycle.GetProperty("cycleNumber").GetInt32());
        Assert.Equal(100m, cycle.GetProperty("collectedAmount").GetDecimal());
    }
}
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.1: the authorization matrix, checked against the real database
[Collection("Postgres collection")]
public class StokvelAuthorizationTests(PostgresApiFactory factory)
{
    // ---- helpers ----

    private static async Task<Guid> CreateUserAsync()
    {
        await using var db = TestDb.NewDb();
        var user = new User("Test User", $"u-{Guid.NewGuid()}@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // a stokvel + one cycle + memberships with the roles you ask for (users must already exist)
    private static async Task<(Guid StokvelId, Guid CycleId)> SeedStokvelAsync(
        params (Guid UserId, StokvelRole Role)[] members)
    {
        await using var db = TestDb.NewDb();

        var stokvel = new Stokvel($"Auth {Guid.NewGuid()}", 100m, ContributionFrequency.Monthly, 5);
        db.Stokvels.Add(stokvel);
        await db.SaveChangesAsync();                       // parent first

        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        var position = 1;
        foreach (var (userId, role) in members)
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, userId, position++, role));
        db.ContributionCycles.Add(cycle);
        await db.SaveChangesAsync();

        return (stokvel.Id, cycle.Id);
    }

    private static async Task AddContributionAsync(Guid stokvelId, Guid userId, Guid cycleId)
    {
        await using var db = TestDb.NewDb();
        db.Contributions.Add(new Contribution(stokvelId, userId, cycleId, 100m));
        await db.SaveChangesAsync();
    }

    private static async Task RemoveMembershipAsync(Guid stokvelId, Guid userId)
    {
        await using var db = TestDb.NewDb();
        await db.StokvelMembers.Where(m => m.StokvelId == stokvelId && m.UserId == userId).ExecuteDeleteAsync();
    }

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, Guid stokvelId, Guid userId, Guid cycleId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { UserId = userId, CycleId = cycleId, Amount = 100m })
        };
        request.Headers.Add("Idempotency-Key", $"k-{Guid.NewGuid()}");
        return client.SendAsync(request);
    }

    // the endpoint used for the 404 / 403 / 201 comparison: open a cycle (officer only)
    private static Task<HttpResponseMessage> OpenCycleAsync(HttpClient client, Guid stokvelId) =>
        client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { CycleNumber = 2, TargetAmount = 100m });

    // ---- 401 ----

    [Fact]
    public async Task Request_With_No_Token_Returns_401_Problem()
    {
        var response = await factory.CreateAnonymousClient().GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Request_With_A_Garbage_Token_Returns_401_Problem()
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real-token");

        var response = await client.GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---- 404 / 403 / success on the SAME endpoint ----

    [Fact]
    public async Task Non_Member_Gets_404_On_An_Officer_Endpoint()
    {
        var outsider = await CreateUserAsync();
        var (stokvelId, _) = await SeedStokvelAsync();

        var response = await OpenCycleAsync(factory.CreateClientFor(outsider), stokvelId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Member_Without_The_Role_Gets_403_On_The_Same_Endpoint()
    {
        var member = await CreateUserAsync();
        var (stokvelId, _) = await SeedStokvelAsync((member, StokvelRole.Member));

        var response = await OpenCycleAsync(factory.CreateClientFor(member), stokvelId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(StokvelRole.Treasurer)]
    [InlineData(StokvelRole.Admin)]   // Admin is the chairperson role from 5.2
    public async Task Treasurer_Or_Chairperson_Succeeds_On_The_Same_Endpoint(StokvelRole role)
    {
        var officer = await CreateUserAsync();
        var (stokvelId, _) = await SeedStokvelAsync((officer, role));

        var response = await OpenCycleAsync(factory.CreateClientFor(officer), stokvelId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Platform_Admin_Succeeds_On_The_Same_Endpoint()
    {
        var (stokvelId, _) = await SeedStokvelAsync();

        var response = await OpenCycleAsync(factory.CreateClient(), stokvelId);   // default client = platform admin

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- refusal must not leak ----

    [Fact]
    public async Task Non_Member_404_Looks_The_Same_As_A_Stokvel_That_Does_Not_Exist()
    {
        var outsider = await CreateUserAsync();
        var (realId, _) = await SeedStokvelAsync();
        var fakeId = Guid.NewGuid();
        var client = factory.CreateClientFor(outsider);

        var real = await client.GetAsync($"/api/v1/stokvels/{realId}");
        var fake = await client.GetAsync($"/api/v1/stokvels/{fakeId}");

        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fake.StatusCode);
        Assert.Equal("application/problem+json", real.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/problem+json", fake.Content.Headers.ContentType?.MediaType);

        // same status, title and detail once the id is blanked out
        var (realStatus, realTitle, realDetail) = await ProblemJson.ReadAsync(real);
        var (fakeStatus, fakeTitle, fakeDetail) = await ProblemJson.ReadAsync(fake);
        Assert.Equal(realStatus, fakeStatus);
        Assert.Equal(realTitle, fakeTitle);
        Assert.Equal(realDetail!.Replace(realId.ToString(), "{id}"), fakeDetail!.Replace(fakeId.ToString(), "{id}"));
    }

    // ---- what a member may do ----

    [Fact]
    public async Task Member_Can_View_Stokvel_Cycles_And_Contributions()
    {
        var member = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((member, StokvelRole.Member));
        var client = factory.CreateClientFor(member);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}/cycles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions")).StatusCode);
    }

    [Fact]
    public async Task Member_Records_Their_Own_Contribution()
    {
        var member = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((member, StokvelRole.Member));

        var response = await RecordAsync(factory.CreateClientFor(member), stokvelId, member, cycleId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Member_Is_Refused_When_Recording_Someone_Elses_Contribution()
    {
        var member = await CreateUserAsync();
        var other = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((member, StokvelRole.Member), (other, StokvelRole.Member));

        var response = await RecordAsync(factory.CreateClientFor(member), stokvelId, other, cycleId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Treasurer_Can_Record_A_Contribution_For_Any_Member()
    {
        var treasurer = await CreateUserAsync();
        var member = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((treasurer, StokvelRole.Treasurer), (member, StokvelRole.Member));

        var response = await RecordAsync(factory.CreateClientFor(treasurer), stokvelId, member, cycleId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- payouts ----

    [Fact]
    public async Task Member_Cannot_Process_A_Payout_But_A_Treasurer_Can()
    {
        var treasurer = await CreateUserAsync();
        var member = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((treasurer, StokvelRole.Treasurer), (member, StokvelRole.Member));
        await AddContributionAsync(stokvelId, member, cycleId);   // a payout needs at least one contribution

        var url = $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts";

        var asMember = await factory.CreateClientFor(member).PostAsync(url, null);
        Assert.Equal(HttpStatusCode.Forbidden, asMember.StatusCode);

        var asTreasurer = await factory.CreateClientFor(treasurer).PostAsync(url, null);
        Assert.Equal(HttpStatusCode.Created, asTreasurer.StatusCode);
    }

    [Fact]
    public async Task Non_Member_Gets_404_On_The_Payout_Endpoint()
    {
        var outsider = await CreateUserAsync();
        var member = await CreateUserAsync();
        var (stokvelId, cycleId) = await SeedStokvelAsync((member, StokvelRole.Treasurer));

        var response = await factory.CreateClientFor(outsider)
            .PostAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payouts", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- the core design point ----

    [Fact]
    public async Task Removed_Member_With_A_Still_Valid_Token_Is_Refused_At_Once()
    {
        var treasurer = await CreateUserAsync();
        var (stokvelId, _) = await SeedStokvelAsync((treasurer, StokvelRole.Treasurer));
        var client = factory.CreateClientFor(treasurer);   // ONE token, used before and after

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);

        await RemoveMembershipAsync(stokvelId, treasurer);   // the chairperson removes them

        // same token, still not expired, but the database says they are out
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/stokvels/{stokvelId}")).StatusCode);
    }
}
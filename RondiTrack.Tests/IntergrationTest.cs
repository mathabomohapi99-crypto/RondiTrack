using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RondiTrack.Tests;

public class IntegrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_User_With_Valid_Data_Returns_201()
    {
        var payload = new { FullName = "Zanele Mthembu", Email = $"zanele{Guid.NewGuid()}@example.com" };
        var response = await _client.PostAsJsonAsync("/api/users", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_Stokvel_With_Valid_Data_Returns_201()
    {
        var payload = new { Name = $"Test Stokvel {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 };
        var response = await _client.PostAsJsonAsync("/api/stokvels", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_Stokvel_With_MaxMembers_Below_Minimum_Returns_400()
    {
        var payload = new { Name = "Bad Stokvel", ContributionAmount = 100m, Frequency = 1, MaxMembers = 1 };
        var response = await _client.PostAsJsonAsync("/api/stokvels", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_Stokvel_That_Does_Not_Exist_Returns_404()
    {
        var response = await _client.GetAsync($"/api/stokvels/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddMember_To_Full_Stokvel_Returns_409()
    {
        var stokvel = await CreateStokvelAsync(2);
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();
        var user3 = await CreateUserAsync();

        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = user1.Id });
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = user2.Id });
        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = user3.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AddMember_With_NonExistent_User_Returns_422()
    {
        var stokvel = await CreateStokvelAsync(5);
        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Repeated_Contribution_Request_With_Same_Key_Returns_Identical_Response()
    {
        var (stokvelId, userId, cycleId) = await SeedStokvelWithMemberAndCycleAsync();
        var key = $"idem-{Guid.NewGuid()}";
        var payload = new { UserId = userId, CycleId = cycleId, Amount = 100m };

        var response1 = await SendContributionAsync(stokvelId, payload, key);
        var body1 = await response1.Content.ReadAsStringAsync();

        var response2 = await SendContributionAsync(stokvelId, payload, key);
        var body2 = await response2.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode);
        Assert.Equal(body1, body2);
    }

    [Fact]
    public async Task Same_Idempotency_Key_With_Different_Payload_Returns_409()
    {
        var (stokvelId, userId, cycleId) = await SeedStokvelWithMemberAndCycleAsync();
        var key = $"idem-{Guid.NewGuid()}";

        await SendContributionAsync(stokvelId, new { UserId = userId, CycleId = cycleId, Amount = 100m }, key);
        var response2 = await SendContributionAsync(stokvelId, new { UserId = userId, CycleId = cycleId, Amount = 200m }, key);

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
        Assert.Equal("application/problem+json", response2.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Second_Contribution_For_Same_User_And_Cycle_With_New_Key_Returns_409()
    {
        var (stokvelId, userId, cycleId) = await SeedStokvelWithMemberAndCycleAsync();
        var payload = new { UserId = userId, CycleId = cycleId, Amount = 100m };

        await SendContributionAsync(stokvelId, payload, $"key-{Guid.NewGuid()}");
        var response2 = await SendContributionAsync(stokvelId, payload, $"key-{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task Contribution_Request_Missing_Idempotency_Key_Returns_400()
    {
        var (stokvelId, userId, cycleId) = await SeedStokvelWithMemberAndCycleAsync();
        var payload = new { UserId = userId, CycleId = cycleId, Amount = 100m };
        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvelId}/contributions", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Edge cases ----

    [Fact]
    public async Task GetMembers_For_Stokvel_With_No_Members_Returns_Empty_List()
    {
        var stokvel = await CreateStokvelAsync(5);
        var response = await _client.GetAsync($"/api/stokvels/{stokvel.Id}/members");
        var members = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(members!);
    }

    [Fact]
    public async Task Create_Stokvel_With_MaxMembers_At_Exactly_The_Minimum_Boundary_Succeeds()
    {
        var payload = new { Name = $"Boundary Stokvel {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = 2 };
        var response = await _client.PostAsJsonAsync("/api/stokvels", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RecordContribution_For_Cycle_Belonging_To_A_Different_Stokvel_Returns_422()
    {
        var (stokvelAId, userId, _) = await SeedStokvelWithMemberAndCycleAsync();
        var (_, _, cycleBId) = await SeedStokvelWithMemberAndCycleAsync();

        var payload = new { UserId = userId, CycleId = cycleBId, Amount = 100m };
        var response = await SendContributionAsync(stokvelAId, payload, $"key-{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---- Helpers ----

    private async Task<HttpResponseMessage> SendContributionAsync(Guid stokvelId, object payload, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Idempotency-Key", key);
        return await _client.SendAsync(request);
    }

    private async Task<StokvelResponseDto> CreateStokvelAsync(int maxMembers)
    {
        var payload = new { Name = $"Stokvel {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = maxMembers };
        var response = await _client.PostAsJsonAsync("/api/stokvels", payload);
        return (await response.Content.ReadFromJsonAsync<StokvelResponseDto>())!;
    }

    private async Task<UserResponseDto> CreateUserAsync()
    {
        var payload = new { FullName = $"Test User {Guid.NewGuid()}", Email = $"user{Guid.NewGuid()}@example.com" };
        var response = await _client.PostAsJsonAsync("/api/users", payload);
        return (await response.Content.ReadFromJsonAsync<UserResponseDto>())!;
    }

    private async Task<(Guid StokvelId, Guid UserId, Guid CycleId)> SeedStokvelWithMemberAndCycleAsync()
    {
        var stokvel = await CreateStokvelAsync(5);
        var user = await CreateUserAsync();
        await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/members", new { UserId = user.Id });

        var cyclePayload = new { CycleNumber = 1, TargetAmount = 100m };
        var cycleResponse = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles", cyclePayload);
        var cycle = (await cycleResponse.Content.ReadFromJsonAsync<CycleResponseDto>())!;

        return (stokvel.Id, user.Id, cycle.Id);
    }

    private sealed record StokvelResponseDto(Guid Id);
    private sealed record UserResponseDto(Guid Id);
    private sealed record CycleResponseDto(Guid Id);
}
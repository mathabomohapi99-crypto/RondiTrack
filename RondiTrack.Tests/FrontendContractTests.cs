using RondiTrack.Tests.TestSupport;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Tests;

// 6.3: what a BROWSER needs: CORS, 429 contract, idempotency under 429, home screen, error contract
// ADJUST: if your other tests get the factory differently (collection fixture), change this constructor.
[Collection("Postgres collection")]
public class FrontendContractTests(PostgresApiFactory factory)
{
    // 6.3: each test builds its OWN host with its OWN settings, so it never uses up the shared allowance
    private WebApplicationFactory<Program> With(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b => { foreach (var (k, v) in settings) b.UseSetting(k, v); });

    private static HttpClient AuthedClient(WebApplicationFactory<Program> f, Guid userId)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestSeed.Token(f.Services, userId));
        return c;
    }

    private static HttpRequestMessage ContributionPost(Guid stokvelId, Guid userId, Guid cycleId, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { userId, cycleId, amount = 100m })
        };
        req.Headers.Add("Idempotency-Key", key);
        return req;
    }

    // ---------------- CORS ----------------
    [Fact]
    public async Task Preflight_from_frontend_with_idempotency_key_is_allowed_with_credentials()
    {
        using var f = With(("Cors:AllowedOrigins:0", "http://localhost:3000"));
        var req = new HttpRequestMessage(HttpMethod.Options, $"/api/v1/stokvels/{Guid.NewGuid()}/contributions");
        req.Headers.Add("Origin", "http://localhost:3000");
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "idempotency-key,content-type,authorization");

        var res = await f.CreateClient().SendAsync(req);

        Assert.True(res.IsSuccessStatusCode);
        Assert.Equal("http://localhost:3000", res.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", res.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("idempotency-key",
            string.Join(",", res.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_from_another_origin_gets_no_cors_headers()
    {
        using var f = With(("Cors:AllowedOrigins:0", "http://localhost:3000"));
        var req = new HttpRequestMessage(HttpMethod.Options, $"/api/v1/stokvels/{Guid.NewGuid()}/contributions");
        req.Headers.Add("Origin", "http://evil.example");
        req.Headers.Add("Access-Control-Request-Method", "POST");

        var res = await f.CreateClient().SendAsync(req);

        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Stokvel_scoped_404_with_an_origin_carries_cors_header()
    {
        using var f = With(("Cors:AllowedOrigins:0", "http://localhost:3000"));
        var outsider = Guid.NewGuid();                                     // signed in, member of nothing
        var client = AuthedClient(f, outsider);
        var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stokvels/{Guid.NewGuid()}/cycles");
        req.Headers.Add("Origin", "http://localhost:3000");

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("http://localhost:3000", res.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    // ---------------- Rate limiting ----------------
    [Fact]
    public async Task Login_returns_429_problem_json_with_retry_after_once_limit_is_passed()
    {
        using var f = With(("RateLimiting:Login:PermitLimit", "2"), ("RateLimiting:Login:WindowSeconds", "60"));
        var client = f.CreateClient();
        object body = new { email = "nobody@test.example", password = "wrong-password-123" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", body)).StatusCode);
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", body);   // 3rd = over the limit

        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.True(res.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("title", out _));
        Assert.True(doc.RootElement.TryGetProperty("detail", out _));
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact]
    public async Task Rejected_request_does_not_use_its_idempotency_key()
    {
        var userId = await TestSeed.UserAsync(factory.Services);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(factory.Services, [(userId, StokvelRole.Member)], cycles: 2);

        // 1 token, refills 1 every 2 seconds
        using var f = With(("RateLimiting:Contribution:TokenLimit", "1"),
                           ("RateLimiting:Contribution:TokensPerPeriod", "1"),
                           ("RateLimiting:Contribution:ReplenishSeconds", "2"));
        var client = AuthedClient(f, userId);

        // 1) uses the only token (cycle 1)
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(ContributionPost(stokvelId, userId, cycles[0], "key-first"))).StatusCode);

        // 2) key K is rejected with 429 (must NOT be stored)
        var k = $"key-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(ContributionPost(stokvelId, userId, cycles[1], k))).StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(2.5));   // wait out the short window

        // 3) same K now succeeds exactly once
        var ok = await client.SendAsync(ContributionPost(stokvelId, userId, cycles[1], k));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.False(ok.Headers.Contains("Idempotency-Replayed"));

        // 4) third time = replay, not a second contribution
        await Task.Delay(TimeSpan.FromSeconds(2.5));   // let the bucket refill so it reaches the idempotency check
        var replay = await client.SendAsync(ContributionPost(stokvelId, userId, cycles[1], k));
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());

        // 5) exactly one row for that cycle
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(1, await db.Contributions.CountAsync(c => c.CycleId == cycles[1] && c.UserId == userId));
    }

    [Fact]
    public async Task One_users_money_limit_does_not_affect_another_user()
    {
        var u1 = await TestSeed.UserAsync(factory.Services);
        var u2 = await TestSeed.UserAsync(factory.Services);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(factory.Services,
            [(u1, StokvelRole.Member), (u2, StokvelRole.Member)], cycles: 2);

        using var f = With(("RateLimiting:Contribution:TokenLimit", "1"),
                           ("RateLimiting:Contribution:TokensPerPeriod", "1"),
                           ("RateLimiting:Contribution:ReplenishSeconds", "60"));
        var c1 = AuthedClient(f, u1);
        var c2 = AuthedClient(f, u2);

        Assert.Equal(HttpStatusCode.Created, (await c1.SendAsync(ContributionPost(stokvelId, u1, cycles[0], $"k-{Guid.NewGuid():N}"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await c1.SendAsync(ContributionPost(stokvelId, u1, cycles[1], $"k-{Guid.NewGuid():N}"))).StatusCode);
        // user 2 still has a full bucket
        Assert.Equal(HttpStatusCode.Created, (await c2.SendAsync(ContributionPost(stokvelId, u2, cycles[0], $"k-{Guid.NewGuid():N}"))).StatusCode);
    }

    // ---------------- Home screen ----------------
    [Fact]
    public async Task Home_shows_both_stokvels_with_the_right_role_in_each()
    {
        var user = await TestSeed.UserAsync(factory.Services);
        var other = await TestSeed.UserAsync(factory.Services);
        var (s1, _) = await TestSeed.StokvelAsync(factory.Services, [(user, StokvelRole.Treasurer), (other, StokvelRole.Member)]);
        var (s2, _) = await TestSeed.StokvelAsync(factory.Services, [(user, StokvelRole.Member), (other, StokvelRole.Treasurer)]);

        var res = await AuthedClient(factory, user).GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var roles = doc.RootElement.GetProperty("stokvels").EnumerateArray()
            .ToDictionary(x => x.GetProperty("stokvelId").GetGuid(), x => x.GetProperty("role").GetString());
        Assert.Equal(2, roles.Count);
        Assert.Equal("Treasurer", roles[s1]);
        Assert.Equal("Member", roles[s2]);
    }

    [Fact]
    public async Task Home_for_a_user_in_no_stokvels_is_an_empty_list_not_404()
    {
        var res = await AuthedClient(factory, Guid.NewGuid()).GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("stokvels").GetArrayLength());
    }

    // ---------------- Error contract ----------------
    [Fact]
    public async Task Validation_failure_returns_errors_map_keyed_by_camelCase_field()
    {
        var client = AuthedClient(factory, Guid.NewGuid());
        var json = """{"name":"","contributionAmount":-5,"frequency":"Monthly","maxMembers":5}""";

        var res = await client.PostAsync("/api/v1/stokvels", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Number_sent_as_string_is_400_without_internal_type_names()
    {
        var client = AuthedClient(factory, Guid.NewGuid());
        var json = """{"name":"X","contributionAmount":100,"frequency":"Monthly","maxMembers":"5"}""";

        var res = await client.PostAsync("/api/v1/stokvels", new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("Int32", body);
    }

    // ---------------- Refresh at the versioned path ----------------
    [Fact]
    public async Task Refresh_works_at_versioned_path()
    {
        using var f = With(("RateLimiting:Login:PermitLimit", "1000"));   // don't trip the login limit
        var client = f.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.example";
        const string password = "Correct-Horse-9!";

        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Refresh Tester", email, password })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rondi_refresh="));
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);   // cookie moved with the routes
        var value = cookie.Split(';')[0]["rondi_refresh=".Length..];

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.TryAddWithoutValidation("Cookie", $"rondi_refresh={value}");   // test host is http, Secure cookie isn't auto-sent
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(req)).StatusCode);
    }
}
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.1: register, login, refresh rotation, reuse detection, logout
[Collection("Postgres collection")]
public class AuthenticationTests(PostgresApiFactory factory)
{
    private const string TestPassword = "a-long-test-password-123";   // throwaway test value, not a real secret
    private const string CookieName = "rondi_refresh";

    private readonly HttpClient _client = factory.CreateAnonymousClient();

    // ---- helpers ----

    private async Task<(string Email, Guid UserId)> RegisterAsync(string? email = null)
    {
        email ??= $"auth{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { FullName = "Auth Tester", Email = email, Password = TestPassword });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (email, doc.RootElement.GetProperty("id").GetGuid());
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password = TestPassword) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });

    // POST with the refresh cookie set by hand
    private Task<HttpResponseMessage> PostWithCookieAsync(string url, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (cookie is not null) request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        return _client.SendAsync(request);
    }

    private static string? CookieValue(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        var line = values.FirstOrDefault(v => v.StartsWith($"{CookieName}="));
        return line?.Split(';')[0][(CookieName.Length + 1)..];
    }

    private static async Task<string> AccessTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    // ---- registration ----

    [Fact]
    public async Task Register_With_Short_Password_Returns_400_Problem()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { FullName = "Short Pass", Email = $"short{Guid.NewGuid():N}@example.com", Password = "tooshort" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_Same_Email_With_Different_Casing_Returns_409()
    {
        var email = $"Lerato{Guid.NewGuid():N}@X.com";
        await RegisterAsync(email);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { FullName = "Lerato Again", Email = email.ToLowerInvariant(), Password = TestPassword });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    // ---- login ----

    [Fact]
    public async Task Login_Returns_Access_Token_In_Body_And_Refresh_Token_Only_In_Cookie()
    {
        var (email, userId) = await RegisterAsync();

        var response = await LoginAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookie = CookieValue(response);
        Assert.False(string.IsNullOrEmpty(cookie));

        // the refresh token must NOT be in the body
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(cookie!, body);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);

        // cookie flags
        var setCookie = response.Headers.GetValues("Set-Cookie").First(v => v.StartsWith($"{CookieName}=")).ToLowerInvariant();
        Assert.Contains("httponly", setCookie);
        Assert.Contains("secure", setCookie);
        Assert.Contains("samesite=strict", setCookie);
        Assert.Contains("path=/api/v1/auth", setCookie);

        // the access token really works: own account 200, someone else's 403
        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;

        var own = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{userId}");
        own.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(own)).StatusCode);

        var other = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/users/{Guid.NewGuid()}");
        other.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(other)).StatusCode);
    }

    [Fact]
    public async Task Wrong_Password_And_Unknown_Email_Return_Identical_Problem_Details()
    {
        var (email, _) = await RegisterAsync();

        var wrongPassword = await LoginAsync(email, "totally-wrong-password-1");
        var unknownEmail = await LoginAsync($"nobody{Guid.NewGuid():N}@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("application/problem+json", wrongPassword.Content.Headers.ContentType?.MediaType);

        // same status, title and detail (only the correlationId differs)
        Assert.Equal(await ProblemJson.ReadAsync(wrongPassword), await ProblemJson.ReadAsync(unknownEmail));
    }

    // ---- refresh ----

    [Fact]
    public async Task Refresh_Rotates_The_Token()
    {
        var (email, _) = await RegisterAsync();
        var login = await LoginAsync(email);
        var first = CookieValue(login)!;

        var refreshed = await PostWithCookieAsync("/api/v1/auth/refresh", first);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        var second = CookieValue(refreshed)!;
        Assert.NotEqual(first, second);                         // a NEW refresh token
        Assert.False(string.IsNullOrEmpty(await AccessTokenAsync(refreshed)));

        // the new token works too
        var again = await PostWithCookieAsync("/api/v1/auth/refresh", second);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Replaying_An_Old_Refresh_Token_Returns_401_And_Kills_The_Newer_Token()
    {
        var (email, _) = await RegisterAsync();
        var oldToken = CookieValue(await LoginAsync(email))!;

        var rotated = await PostWithCookieAsync("/api/v1/auth/refresh", oldToken);
        var newToken = CookieValue(rotated)!;

        // replay the OLD token: 401
        var replay = await PostWithCookieAsync("/api/v1/auth/refresh", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("application/problem+json", replay.Content.Headers.ContentType?.MediaType);

        // the NEWER token is now dead as well (whole family revoked)
        var afterReuse = await PostWithCookieAsync("/api/v1/auth/refresh", newToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_Without_A_Cookie_Returns_401()
    {
        var response = await PostWithCookieAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // ---- logout ----

    [Fact]
    public async Task After_Logout_Refreshing_Returns_401()
    {
        var (email, _) = await RegisterAsync();
        var token = CookieValue(await LoginAsync(email))!;

        var logout = await PostWithCookieAsync("/api/v1/auth/logout", token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await PostWithCookieAsync("/api/v1/auth/refresh", token);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_Is_Idempotent()
    {
        var (email, _) = await RegisterAsync();
        var token = CookieValue(await LoginAsync(email))!;

        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/api/v1/auth/logout", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/api/v1/auth/logout", token)).StatusCode);     // same token again
        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookieAsync("/api/v1/auth/logout", null)).StatusCode);      // no cookie at all
    }

    // ---- secure by default ----

    [Fact]
    public async Task OpenApi_And_Scalar_Load_Without_A_Token()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/scalar/v1")).StatusCode);
    }
}
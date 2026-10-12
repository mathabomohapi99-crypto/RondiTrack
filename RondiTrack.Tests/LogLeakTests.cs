using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 6.2: the log-leak test. No personal or secret data may reach a log, and a 409 is not an error.
[Collection("Postgres collection")]
public class LogLeakTests(PostgresApiFactory factory)
{
    private const string CookieName = "rondi_refresh";
    private const string Amount = "1234.56";   // a distinctive amount to search for

    private readonly HttpClient _client = factory.CreateAnonymousClient();

    private sealed record IdDto(Guid Id);
    private sealed record TokenDto(string AccessToken);

    // reads the refresh token out of the Set-Cookie header
    private static string? CookieValue(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        var line = values.FirstOrDefault(v => v.StartsWith($"{CookieName}="));
        return line?.Split(';')[0][(CookieName.Length + 1)..];
    }

    [Fact]
    public async Task No_Log_Line_Contains_Personal_Or_Secret_Data_And_A_409_Is_Not_An_Error()
    {
        var logStart = factory.CapturedLogs.Entries.Count;   // only look at lines written by THIS test
        var email = $"leak{Guid.NewGuid():N}@example.com";
        var password = $"pw-{Guid.NewGuid():N}-xyz";

        // 1. register
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { FullName = "Leak Tester", Email = email, Password = password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<IdDto>())!.Id;

        // 2. log in
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var accessToken1 = (await login.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;
        var refreshToken1 = CookieValue(login)!;

        // 3. refresh (rotates both tokens)
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"{CookieName}={refreshToken1}");
        var refresh = await _client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var accessToken2 = (await refresh.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;
        var refreshToken2 = CookieValue(refresh)!;

        Assert.False(string.IsNullOrEmpty(accessToken1));   // so we never search for an empty string
        Assert.False(string.IsNullOrEmpty(refreshToken2));

        // 4. record a contribution with the distinctive amount (user joins a fresh stokvel)
        Guid stokvelId, cycleId;
        await using (var db = TestDb.NewDb())
        {
            var stokvel = new Stokvel($"Leak {Guid.NewGuid()}", 100m, ContributionFrequency.Monthly, 5);
            db.Stokvels.Add(stokvel);
            await db.SaveChangesAsync();   // parent first

            var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
            db.StokvelMembers.Add(new StokvelMember(stokvel.Id, userId, 1));
            db.ContributionCycles.Add(cycle);
            await db.SaveChangesAsync();
            stokvelId = stokvel.Id;
            cycleId = cycle.Id;
        }

        var contribution = new HttpRequestMessage(HttpMethod.Post, $"/api/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new { UserId = userId, CycleId = cycleId, Amount = 1234.56m })
        };
        contribution.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken2);
        contribution.Headers.Add("Idempotency-Key", $"leak-{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(contribution)).StatusCode);

        // 5. trigger the duplicate-registration 409
        var beforeConflict = factory.CapturedLogs.Entries.Count;   // remember where the 409 logs start
        var duplicate = await _client.PostAsJsonAsync("/api/auth/register",
            new { FullName = "Leak Again", Email = email, Password = password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var all = factory.CapturedLogs.Entries.Skip(logStart).ToList();   // all lines from this test

        // the 409 must not be an Error, and the handler must have logged it at Information
        var forConflict = factory.CapturedLogs.Entries.Skip(beforeConflict).ToList();
        Assert.DoesNotContain(forConflict, e => e.Level >= LogLevel.Error);
        Assert.Contains(forConflict, e => e.Level == LogLevel.Information && e.Message.Contains("409"));

        // nothing secret or personal anywhere in the captured logs
        var secrets = new[] { email, password, accessToken1, accessToken2, refreshToken1, refreshToken2, Amount, "1234,56" };
        foreach (var entry in all)
            foreach (var secret in secrets)
                Assert.DoesNotContain(secret, entry.AllText, StringComparison.OrdinalIgnoreCase);
    }
}
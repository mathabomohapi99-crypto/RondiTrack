using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class CloseCyclePreviewTests(PostgresApiFactory factory)
{
    private IServiceProvider Sp => factory.Services;

    // Treasurer paid 0 (owes 500), "Full" paid 500 (owes 0), "Part" paid 200 (owes 300). Total arrears R 800,00.
    private sealed record Setup(Guid StokvelId, Guid CycleId, Guid Treasurer, Guid Full, Guid Part);

    private async Task<Setup> BuildAsync()
    {
        var t = await TestSeed.UserAsync(Sp); var full = await TestSeed.UserAsync(Sp); var part = await TestSeed.UserAsync(Sp);
        var (s, cycles) = await TestSeed.StokvelAsync(Sp, [(t, StokvelRole.Treasurer), (full, StokvelRole.Member), (part, StokvelRole.Member)], amount: 500m);
        await W9.AddContributionAsync(s, full, cycles[0], 500m);
        await W9.AddContributionAsync(s, part, cycles[0], 200m);
        return new Setup(s, cycles[0], t, full, part);
    }

    private static string Base(Setup x) => $"/api/v1/stokvels/{x.StokvelId}/cycles/{x.CycleId}";

    private static HttpRequestMessage Close(Setup x, string version, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{Base(x)}/close") { Content = JsonContent.Create(new { version }) };
        req.Headers.Add("Idempotency-Key", key);
        return req;
    }

    private async Task<JsonElement> PreviewAsync(Setup x) =>
        await factory.CreateClientFor(x.Treasurer).GetFromJsonAsync<JsonElement>($"{Base(x)}/close-preview");

    private static async Task<int> ArrearCountAsync(Guid cycleId)
    {
        await using var db = TestDb.NewDb();
        return await db.Arrears.CountAsync(a => a.CycleId == cycleId);
    }

    [Fact]
    public async Task Preview_lists_who_owes_what_as_of_the_south_african_date()
    {
        var x = await BuildAsync();
        var p = await PreviewAsync(x);

        Assert.Equal(80000, p.GetProperty("totalArrearsCents").GetInt64());
        var shortfalls = p.GetProperty("arrears").EnumerateArray().Select(a => a.GetProperty("shortfallCents").GetInt64()).Order().ToArray();
        Assert.Equal(new long[] { 30000, 50000 }, shortfalls);
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}$"), p.GetProperty("asOfDate").GetString()!);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)).ToString("yyyy-MM-dd"), p.GetProperty("asOfDate").GetString());
    }

    [Fact]
    public async Task A_member_who_is_not_a_treasurer_gets_403()
    {
        var x = await BuildAsync();
        var member = factory.CreateClientFor(x.Full);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"{Base(x)}/close-preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.SendAsync(Close(x, "abc", "k-" + Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task The_close_records_exactly_what_the_preview_showed()
    {
        var x = await BuildAsync();
        var p = await PreviewAsync(x);

        var res = await factory.CreateClientFor(x.Treasurer).SendAsync(Close(x, p.GetProperty("version").GetString()!, "k-" + Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var done = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(80000, done.GetProperty("totalArrearsCents").GetInt64());
        Assert.Equal(2, done.GetProperty("arrearsRecorded").GetInt32());
        await using var db = TestDb.NewDb();
        Assert.Equal(80000, await db.Arrears.Where(a => a.CycleId == x.CycleId).SumAsync(a => a.ShortfallCents));
    }

    [Fact]
    public async Task A_stale_preview_is_refused_with_409_and_nothing_is_recorded()
    {
        var x = await BuildAsync();
        var seen = await PreviewAsync(x);

        await W9.AddContributionAsync(x.StokvelId, x.Treasurer, x.CycleId, 100m);   // recorded AFTER the preview

        var stale = await factory.CreateClientFor(x.Treasurer).SendAsync(Close(x, seen.GetProperty("version").GetString()!, "k-" + Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("stale_preview", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, await ArrearCountAsync(x.CycleId));

        // the NEW preview shows the new numbers, and confirming that one works
        var fresh = await PreviewAsync(x);
        Assert.NotEqual(seen.GetProperty("version").GetString(), fresh.GetProperty("version").GetString());
        Assert.Equal(70000, fresh.GetProperty("totalArrearsCents").GetInt64());   // 400 + 300
        var ok = await factory.CreateClientFor(x.Treasurer).SendAsync(Close(x, fresh.GetProperty("version").GetString()!, "k-" + Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(2, await ArrearCountAsync(x.CycleId));
    }

    [Fact]
    public async Task Sending_the_close_twice_records_the_arrears_once()
    {
        var x = await BuildAsync();
        var version = (await PreviewAsync(x)).GetProperty("version").GetString()!;
        var key = "k-" + Guid.NewGuid();
        var treasurer = factory.CreateClientFor(x.Treasurer);

        var first = await treasurer.SendAsync(Close(x, version, key));
        var second = await treasurer.SendAsync(Close(x, version, key));   // a double-click or a retry: SAME key

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues("Idempotency-Replayed").Single());
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalArrearsCents").GetInt64(),
            (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalArrearsCents").GetInt64());
        Assert.Equal(2, await ArrearCountAsync(x.CycleId));

        // a DIFFERENT key on an already closed cycle is a plain 409
        var other = await treasurer.SendAsync(Close(x, version, "k-" + Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal("cycle_closed", (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(2, await ArrearCountAsync(x.CycleId));
    }

    [Fact]
    public async Task Two_closes_at_the_same_moment_record_the_arrears_once()
    {
        var x = await BuildAsync();
        var version = (await PreviewAsync(x)).GetProperty("version").GetString()!;
        var key = "k-" + Guid.NewGuid();
        var treasurer = factory.CreateClientFor(x.Treasurer);

        var both = await Task.WhenAll(treasurer.SendAsync(Close(x, version, key)), treasurer.SendAsync(Close(x, version, key)));

        Assert.All(both, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(2, await ArrearCountAsync(x.CycleId));
    }

    [Fact]
    public async Task A_closed_cycle_takes_no_more_contributions()
    {
        var x = await BuildAsync();
        var version = (await PreviewAsync(x)).GetProperty("version").GetString()!;
        await factory.CreateClientFor(x.Treasurer).SendAsync(Close(x, version, "k-" + Guid.NewGuid()));

        var req = new HttpRequestMessage(HttpMethod.Post, $"{Base(x)}/contributions")
        { Content = JsonContent.Create(new { userId = x.Treasurer, amountInCents = 50000L }) };
        req.Headers.Add("Idempotency-Key", "k-" + Guid.NewGuid());
        var res = await factory.CreateClientFor(x.Treasurer).SendAsync(req);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
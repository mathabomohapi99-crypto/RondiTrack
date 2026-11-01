using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 8.3: history filters, the half-open time range, token safety and the activity feed
[Collection("Postgres collection")]
public class StokvelHistoryTests(PostgresApiFactory factory)
{
    // Records one contribution and stamps it with an exact UTC time
    private static async Task AddAtAsync(Guid stokvelId, Guid userId, Guid cycleId, string whenUtc)
    {
        await using var db = TestDb.NewDb();
        var c = new Contribution(stokvelId, userId, cycleId, 100m);
        db.Contributions.Add(c);
        await db.SaveChangesAsync();
        var when = DateTime.Parse(whenUtc, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        await db.Contributions.Where(x => x.Id == c.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RecordedAtUtc, when));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Root)> GetAsync(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return (res.StatusCode, doc.RootElement.Clone());
    }

    // two members, three cycles (one contribution per cycle per member is allowed)
    private async Task<(Guid A, Guid B, Guid StokvelId, Guid[] Cycles, HttpClient ClientA)> SeedAsync()
    {
        var a = await TestSeed.UserAsync(factory.Services);
        var b = await TestSeed.UserAsync(factory.Services);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(
            factory.Services, [(a, StokvelRole.Member), (b, StokvelRole.Member)], cycles: 3);
        return (a, b, stokvelId, cycles, factory.CreateClientFor(a));
    }

    [Fact]
    public async Task Range_is_half_open_and_keeps_2330_SAST_on_the_last_day()
    {
        var (a, _, stokvelId, cycles, client) = await SeedAsync();
        await AddAtAsync(stokvelId, a, cycles[0], "2026-09-30T22:00:00Z");   // 00:00 SAST 1 Oct: included
        await AddAtAsync(stokvelId, a, cycles[1], "2026-10-31T21:30:00Z");   // 23:30 SAST 31 Oct: included
        await AddAtAsync(stokvelId, a, cycles[2], "2026-10-31T22:00:00Z");   // 00:00 SAST 1 Nov: excluded

        var (status, root) = await GetAsync(client,
            $"/api/v1/stokvels/{stokvelId}/contributions?from=2026-09-30T22:00:00Z&to=2026-10-31T22:00:00Z");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Default_order_is_newest_first_and_page_size_is_respected()
    {
        var (a, _, stokvelId, cycles, client) = await SeedAsync();
        await AddAtAsync(stokvelId, a, cycles[0], "2026-10-01T10:00:00Z");
        await AddAtAsync(stokvelId, a, cycles[1], "2026-10-03T10:00:00Z");
        await AddAtAsync(stokvelId, a, cycles[2], "2026-10-02T10:00:00Z");

        var (_, root) = await GetAsync(client, $"/api/v1/stokvels/{stokvelId}/contributions?pageSize=2");
        var items = root.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
        Assert.True(items[0].GetProperty("recordedAtUtc").GetDateTime() > items[1].GetProperty("recordedAtUtc").GetDateTime());
        Assert.NotEqual("", root.GetProperty("nextPageToken").GetString());
    }

    [Fact]
    public async Task Member_filter_returns_only_that_member()
    {
        var (a, b, stokvelId, cycles, client) = await SeedAsync();
        await AddAtAsync(stokvelId, a, cycles[0], "2026-10-01T10:00:00Z");
        await AddAtAsync(stokvelId, b, cycles[0], "2026-10-02T10:00:00Z");

        var (_, root) = await GetAsync(client, $"/api/v1/stokvels/{stokvelId}/contributions?member={b}");

        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
        Assert.Equal(b, root.GetProperty("items")[0].GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Member_who_is_not_in_this_stokvel_gives_an_empty_page_not_an_error()
    {
        var (a, _, stokvelId, cycles, client) = await SeedAsync();
        await AddAtAsync(stokvelId, a, cycles[0], "2026-10-01T10:00:00Z");

        var (status, root) = await GetAsync(client, $"/api/v1/stokvels/{stokvelId}/contributions?member={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Page_token_from_one_filter_used_with_another_filter_returns_400()
    {
        var (a, _, stokvelId, cycles, client) = await SeedAsync();
        await AddAtAsync(stokvelId, a, cycles[0], "2026-10-01T10:00:00Z");
        await AddAtAsync(stokvelId, a, cycles[1], "2026-10-02T10:00:00Z");

        var (_, first) = await GetAsync(client, $"/api/v1/stokvels/{stokvelId}/contributions?pageSize=1");
        var token = first.GetProperty("nextPageToken").GetString()!;
        Assert.NotEqual("", token);

        var (status, _) = await GetAsync(client,
            $"/api/v1/stokvels/{stokvelId}/contributions?pageSize=1&member={a}&pageToken={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Malformed_date_returns_400()
    {
        var (_, _, stokvelId, _, client) = await SeedAsync();

        var (status, _) = await GetAsync(client, $"/api/v1/stokvels/{stokvelId}/contributions?from=banana");

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task Someone_who_is_not_a_member_gets_404()
    {
        var (_, _, stokvelId, _, _) = await SeedAsync();
        var outsider = await TestSeed.UserAsync(factory.Services);

        var (status, _) = await GetAsync(factory.CreateClientFor(outsider), $"/api/v1/stokvels/{stokvelId}/contributions");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task Activity_has_only_my_stokvels_newest_first_and_ends_with_an_empty_token()
    {
        var me = await TestSeed.UserAsync(factory.Services);
        var other = await TestSeed.UserAsync(factory.Services);
        var (mine, myCycles) = await TestSeed.StokvelAsync(factory.Services, [(me, StokvelRole.Member)], cycles: 2);
        var (theirs, theirCycles) = await TestSeed.StokvelAsync(factory.Services, [(other, StokvelRole.Member)], cycles: 1);
        await AddAtAsync(mine, me, myCycles[0], "2026-10-01T10:00:00Z");
        await AddAtAsync(mine, me, myCycles[1], "2026-10-02T10:00:00Z");
        await AddAtAsync(theirs, other, theirCycles[0], "2026-10-03T10:00:00Z");   // NOT mine: must never show

        var client = factory.CreateClientFor(me);
        var seen = new List<JsonElement>();
        var token = "";
        do
        {
            var url = "/api/v1/me/activity?pageSize=1" + (token == "" ? "" : $"&pageToken={Uri.EscapeDataString(token)}");
            var (status, page) = await GetAsync(client, url);
            Assert.Equal(HttpStatusCode.OK, status);
            foreach (var item in page.GetProperty("items").EnumerateArray()) seen.Add(item);
            token = page.GetProperty("nextPageToken").GetString() ?? "";
        } while (token != "");

        Assert.NotEmpty(seen);
        Assert.All(seen, i => Assert.Equal(mine, i.GetProperty("stokvelId").GetGuid()));
        Assert.Contains(seen, i => i.GetProperty("kind").GetString() == "Contribution");

        var times = seen.Select(i => i.GetProperty("occurredAtUtc").GetDateTime()).ToList();
        Assert.Equal(times.OrderByDescending(x => x).ToList(), times);
    }
}
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

// NEW 7.2: what the styled stokvel view needs from the API
[Collection("Postgres collection")]
public class StokvelViewContractTests(PostgresApiFactory factory)
{
    // a stokvel with two members and three cycles: #1 PaidOut, #2 Closed, #3 Open
    private async Task<(Guid User, Guid Other, Guid Stokvel, Guid[] Cycles)> SeedAsync()
    {
        var user = await TestSeed.UserAsync(factory.Services);
        var other = await TestSeed.UserAsync(factory.Services);
        var (stokvelId, cycles) = await TestSeed.StokvelAsync(factory.Services,
            [(user, StokvelRole.Member), (other, StokvelRole.Member)], cycles: 3, amount: 500m);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        await db.ContributionCycles.Where(c => c.Id == cycles[0])
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CycleStatus.PaidOut));
        await db.ContributionCycles.Where(c => c.Id == cycles[1])
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ClosedAtUtc, (DateTime?)DateTime.UtcNow));
        return (user, other, stokvelId, cycles);
    }

    [Fact]
    public async Task Cycle_list_is_newest_first_with_status_due_date_and_totals_in_cents()
    {
        var (user, _, stokvelId, cycles) = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Contributions.Add(new Contribution(stokvelId, user, cycles[2], 150.50m));
            await db.SaveChangesAsync();
        }

        var res = await factory.CreateClientFor(user).GetAsync($"/api/v1/stokvels/{stokvelId}/cycles");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(new[] { 3, 2, 1 }, items.Select(i => i.GetProperty("number").GetInt32()));
        Assert.Equal(new[] { "Open", "Closed", "PaidOut" }, items.Select(i => i.GetProperty("status").GetString()));
        Assert.Equal(15050, items[0].GetProperty("collectedCents").GetInt64());
        Assert.Equal(0, items[1].GetProperty("collectedCents").GetInt64());
        Assert.Equal(100000, items[0].GetProperty("dueCents").GetInt64());   // R500 x 2 members
        Assert.EndsWith("Z", items[0].GetProperty("endsAt").GetString());     // every timestamp ends in Z
    }

    [Fact]
    public async Task Contributions_carry_cycle_id_cents_and_status_and_no_email()
    {
        var (user, other, stokvelId, cycles) = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Contributions.Add(new Contribution(stokvelId, user, cycles[2], 0.10m));   // 10 cents, Paid by default
            var confirmed = new Contribution(stokvelId, other, cycles[2], 150.50m);
            db.Contributions.Add(confirmed);
            await db.SaveChangesAsync();
            await db.Contributions.Where(c => c.Id == confirmed.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, ContributionStatus.Confirmed));
        }

        var res = await factory.CreateClientFor(user)
            .GetAsync($"/api/v1/stokvels/{stokvelId}/cycles/{cycles[2]}/contributions");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i =>
        {
            Assert.Equal(cycles[2], i.GetProperty("cycleId").GetGuid());
            Assert.False(i.TryGetProperty("memberEmail", out _));   // nothing extra
            Assert.False(i.TryGetProperty("amount", out _));        // cents only
        });
        Assert.Equal(new long[] { 10, 15050 }, items.Select(i => i.GetProperty("amountCents").GetInt64()).Order().ToList());
        Assert.Equal(new[] { "Confirmed", "Paid" }, items.Select(i => i.GetProperty("status").GetString()).Order().ToList());
    }

    [Fact]
    public async Task Home_shows_only_the_callers_own_arrears_and_the_open_cycle_number()
    {
        var (user, other, stokvelId, cycles) = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.Arrears.Add(new Arrear(stokvelId, cycles[1], user, 30000, DateTime.UtcNow));
            db.Arrears.Add(new Arrear(stokvelId, cycles[1], other, 50000, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        async Task<JsonElement> EntryAsync(Guid caller)
        {
            var res = await factory.CreateClientFor(caller).GetAsync("/api/v1/me/home");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("stokvels").EnumerateArray()
                .Single(s => s.GetProperty("stokvelId").GetGuid() == stokvelId).Clone();
        }

        var mine = await EntryAsync(user);
        Assert.Equal(30000, mine.GetProperty("arrearsCents").GetInt64());               // my own, not 80000
        Assert.Equal(50000, (await EntryAsync(other)).GetProperty("arrearsCents").GetInt64());

        var cycle = mine.GetProperty("currentCycle");
        Assert.Equal(3, cycle.GetProperty("number").GetInt32());                         // the closed and paid-out cycles are not current
        Assert.EndsWith("Z", cycle.GetProperty("endsAt").GetString());
    }
}
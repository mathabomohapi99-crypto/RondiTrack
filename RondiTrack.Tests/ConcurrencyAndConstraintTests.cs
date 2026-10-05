using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RondiTrack.Domain;
using RondiTrack.Errors;
using Xunit;

namespace RondiTrack.Tests;

public class ConcurrencyAndConstraintTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ---------- xmin concurrency ----------

    [Fact]
    public async Task Two_contexts_editing_the_same_payout_raise_DbUpdateConcurrencyException()
    {
        var payoutId = await TestDb.SeedPayoutAsync();

        // TWO contexts are required. Each context remembers the xmin it loaded. With ONE context there is
        // only one copy of the row in memory, and after our own save EF adopts the new xmin, so the context
        // can never see "someone else changed it". Conflicts only exist between two separate views of the row.
        await using var ctxA = TestDb.NewDb();
        await using var ctxB = TestDb.NewDb();

        var a = await ctxA.Payouts.SingleAsync(p => p.Id == payoutId);
        var b = await ctxB.Payouts.SingleAsync(p => p.Id == payoutId);   // both loaded the same version

        ctxA.Entry(a).Property(p => p.Amount).CurrentValue = 200m;
        await ctxA.SaveChangesAsync();                                    // first save wins and moves xmin forward

        ctxB.Entry(b).Property(p => p.Amount).CurrentValue = 300m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());   // second is stale
    }

    [Fact]
    public async Task Http_update_with_a_stale_etag_returns_412_problem()
    {
        // Stokvel is used for the HTTP test because it is one of the entities that has an edit endpoint.
        // Payout has no edit endpoint in RondiTrack, and adding one would be a new feature.
        var created = await _client.PostAsJsonAsync("/api/stokvels",
            new { Name = $"Etag {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 });
        var location = created.Headers.Location!;

        var get = await _client.GetAsync(location);                      // GET: read the ETag
        var etag = get.Headers.ETag!.Tag;

        // First update with the token from the GET: must succeed.
        var first = await PutStokvelAsync(location, "First Name", etag);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second update with the SAME (now stale) token: must be 412 as a problem response.
        var second = await PutStokvelAsync(location, "Second Name", etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Http_update_without_if_match_returns_428_problem()
    {
        var created = await _client.PostAsJsonAsync("/api/stokvels",
            new { Name = $"NoEtag {Guid.NewGuid()}", ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 });

        var response = await _client.PutAsJsonAsync(created.Headers.Location!,
            new { Name = "x", ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 });

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private Task<HttpResponseMessage> PutStokvelAsync(Uri url, string name, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(new { Name = name, ContributionAmount = 100m, Frequency = 1, MaxMembers = 5 })
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return _client.SendAsync(request);
    }

    // ---------- unique constraints (straight to the database, no service check) ----------

    [Fact]
    public async Task Database_rejects_a_second_contribution_for_the_same_member_and_cycle()
    {
        var (stokvelId, userId, cycleId) = await TestDb.SeedMemberAndCycleAsync();

        await using (var db = TestDb.NewDb())
        {
            db.Contributions.Add(new Contribution(stokvelId, userId, cycleId, 100m));
            await db.SaveChangesAsync();
        }

        // Straight to the DbContext: ContributionService.ExistsAsync is bypassed, only the constraint is left.
        await using var db2 = TestDb.NewDb();
        db2.Contributions.Add(new Contribution(stokvelId, userId, cycleId, 100m));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task Database_rejects_paying_the_same_member_twice_in_one_stokvel()
    {
        var (stokvelId, userId, cycleId) = await TestDb.SeedMemberAndCycleAsync();

        await using var db = TestDb.NewDb();
        var cycle2 = new ContributionCycle(stokvelId, 2, 100m);
        db.ContributionCycles.Add(cycle2);
        db.Payouts.Add(new Payout(stokvelId, cycleId, userId, 100m));
        await db.SaveChangesAsync();

        // Different cycle, same recipient, same stokvel: PayoutService would refuse, now the database does too.
        await using var db2 = TestDb.NewDb();
        db2.Payouts.Add(new Payout(stokvelId, cycle2.Id, userId, 100m));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    // ---------- the central handler turns 23505 into 409 ----------

    [Fact]
    public async Task Exception_handler_maps_a_unique_violation_to_409()
    {
        var handler = new RondiExceptionHandler(NullLogger<RondiExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var postgres = new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var handled = await handler.TryHandleAsync(context, new DbUpdateException("dup", postgres), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }
}
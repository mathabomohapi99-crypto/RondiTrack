using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Services;
using Xunit;

namespace RondiTrack.Tests;

public class ContributionServiceTests
{
    private static (ContributionService service, IStokvelRepository stokvels, IUserRepository users, IContributionCycleRepository cycles) Build()
    {
        var stokvels = new InMemoryStokvelRepository();
        var users = new InMemoryUserRepository();
        var cycles = new InMemoryContributionCycleRepository();
        var contributions = new InMemoryContributionRepository();
        var idempotency = new InMemoryIdempotencyStore();
        var service = new ContributionService(stokvels, users, cycles, contributions, idempotency);
        return (service, stokvels, users, cycles);
    }

    [Fact]
    public async Task RecordContribution_Throws_When_Same_User_Pays_Same_Cycle_Twice()
    {
        var built = Build();
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("A", "a@example.com");
        stokvel.AddMember(user);
        await built.stokvels.AddAsync(stokvel);
        await built.users.AddAsync(user);
        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        await built.cycles.AddAsync(cycle);

        var request = new ContributionRequest(user.Id, cycle.Id, 100m);
        await built.service.RecordContributionAsync(stokvel.Id, request, "key-1");

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            built.service.RecordContributionAsync(stokvel.Id, request, "key-2"));
    }

    [Fact]
    public async Task RecordContribution_Replays_Same_Response_For_Same_Key_And_Same_Body()
    {
        var built = Build();
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("A", "a@example.com");
        stokvel.AddMember(user);
        await built.stokvels.AddAsync(stokvel);
        await built.users.AddAsync(user);
        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        await built.cycles.AddAsync(cycle);

        var request = new ContributionRequest(user.Id, cycle.Id, 100m);
        var first = await built.service.RecordContributionAsync(stokvel.Id, request, "same-key");
        var second = await built.service.RecordContributionAsync(stokvel.Id, request, "same-key");

        Assert.Equal(first.Response.Id, second.Response.Id);
        Assert.True(second.WasReplayed);
    }

    [Fact]
    public async Task RecordContribution_Throws_When_Same_Key_Used_With_Different_Body()
    {
        var built = Build();
        var stokvel = new Stokvel("Test", 100m, ContributionFrequency.Monthly, 5);
        var user = new User("A", "a@example.com");
        stokvel.AddMember(user);
        await built.stokvels.AddAsync(stokvel);
        await built.users.AddAsync(user);
        var cycle = new ContributionCycle(stokvel.Id, 1, 100m);
        await built.cycles.AddAsync(cycle);

        var request = new ContributionRequest(user.Id, cycle.Id, 100m);
        await built.service.RecordContributionAsync(stokvel.Id, request, "reused-key");

        var differentRequest = request with { Amount = 200m };

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            built.service.RecordContributionAsync(stokvel.Id, differentRequest, "reused-key"));
    }
}
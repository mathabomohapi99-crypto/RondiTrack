using System.Security.Cryptography;
using System.Text;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;
using RondiTrack.Telemetry;   // NEW 6.2

namespace RondiTrack.Services;

public interface IContributionService
{
    Task<(ContributionResponse Response, bool WasReplayed)> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string idempotencyKey);
}

public sealed class ContributionService(
    IStokvelRepository stokvels,
    IUserRepository users,
    IContributionCycleRepository cycles,
    IContributionRepository contributions,
    IIdempotencyStore idempotencyStore) : IContributionService
{
    public async Task<(ContributionResponse Response, bool WasReplayed)> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string idempotencyKey)
    {
        var requestHash = ComputeHash(stokvelId, request);

        var existing = await idempotencyStore.GetAsync(idempotencyKey);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new DomainConflictException("This Idempotency-Key was already used with a different request body.");

            return (existing.Response, true);
        }

        var stokvel = await stokvels.GetByIdAsync(stokvelId)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var user = await users.GetByIdAsync(request.UserId)
            ?? throw new DomainReferenceException($"User {request.UserId} does not exist.");

        if (!stokvel.HasMember(user.Id))
            throw new DomainReferenceException("That user is not a member of this stokvel.");   // CHANGED 6.2: no name, no stokvel name

        var cycle = await cycles.GetByIdAsync(request.CycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainReferenceException($"Cycle {request.CycleId} does not exist for this stokvel.");

        // NEW 7.4: a Closed or PaidOut cycle takes no more money. The form shows this detail if the cycle closes mid-edit.
        if (cycle.Status != CycleStatus.Open)
            throw new DomainConflictException("This cycle is no longer open, so contributions cannot be recorded.");

        if (await contributions.ExistsAsync(stokvelId, user.Id, request.CycleId))
            throw new DomainConflictException("This member has already contributed for this cycle.");   // CHANGED 6.2: no name

        var contribution = new Contribution(stokvelId, user.Id, request.CycleId, request.Amount);
        await contributions.AddAsync(contribution);
        RondiMetrics.ContributionRecorded();   // NEW 6.2: counted only after the row is really saved

        var response = contribution.ToResponse();
        await idempotencyStore.SaveAsync(idempotencyKey, new IdempotencyRecord(requestHash, response));

        return (response, false);
    }

    private static string ComputeHash(Guid stokvelId, ContributionRequest request)
    {
        var raw = $"{stokvelId}|{request.UserId}|{request.CycleId}|{request.Amount}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
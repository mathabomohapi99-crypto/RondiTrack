using System.Security.Cryptography;
using System.Text;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

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
            throw new DomainReferenceException($"{user.FullName} is not a member of '{stokvel.Name}'.");

        var cycle = await cycles.GetByIdAsync(request.CycleId);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new DomainReferenceException($"Cycle {request.CycleId} does not exist for this stokvel.");

        if (await contributions.ExistsAsync(stokvelId, user.Id, request.CycleId))
            throw new DomainConflictException($"{user.FullName} has already contributed for this cycle.");

        var contribution = new Contribution(stokvelId, user.Id, request.CycleId, request.Amount);
        await contributions.AddAsync(contribution);

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
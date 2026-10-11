using RondiTrack.Data;
using RondiTrack.Domain;

namespace RondiTrack.Services;

public interface IStokvelMembershipService
{
    Task<User> AddMemberAsync(Guid stokvelId, Guid userId);
    Task RemoveMemberAsync(Guid stokvelId, Guid userId);
}

public sealed class StokvelMembershipService(IStokvelRepository stokvels, IUserRepository users)
    : IStokvelMembershipService
{
    public async Task<User> AddMemberAsync(Guid stokvelId, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var user = await users.GetByIdAsync(userId)
            ?? throw new DomainReferenceException($"User {userId} does not exist.");

        stokvel.AddMember(user); // still Stokvel's own rule: no duplicates, no exceeding capacity
        await stokvels.UpdateAsync(stokvel);
        return user;
    }

    public async Task RemoveMemberAsync(Guid stokvelId, Guid userId)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId)
            ?? throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        if (!stokvel.RemoveMember(userId))
            throw new DomainNotFoundException($"User {userId} is not a member of this stokvel.");

        await stokvels.UpdateAsync(stokvel);
    }
}
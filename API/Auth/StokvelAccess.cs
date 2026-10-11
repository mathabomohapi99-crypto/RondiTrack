using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Auth;

public interface IStokvelAccess
{
    Task EnsureAsync(ClaimsPrincipal user, Guid stokvelId, string policy, Guid? onBehalfOfUserId = null);
}

// One place that decides 404 vs 403. No try/catch, no role names.
public sealed class StokvelAccess(IAuthorizationService authorization) : IStokvelAccess
{
    public async Task EnsureAsync(ClaimsPrincipal user, Guid stokvelId, string policy, Guid? onBehalfOfUserId = null)
    {
        var result = await authorization.AuthorizeAsync(user, new StokvelResource(stokvelId, onBehalfOfUserId), policy);
        if (result.Succeeded) return;

        var notMember = result.Failure?.FailureReasons
            .Any(r => r.Message == StokvelAccessHandler.NotMemberReason) ?? false;

        // not a member: pretend the stokvel does not exist (same text as the real 404)
        if (notMember)
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        // a member without the role already knows the stokvel exists
        throw new ForbiddenException("You do not have the required role in this stokvel.");
    }
}
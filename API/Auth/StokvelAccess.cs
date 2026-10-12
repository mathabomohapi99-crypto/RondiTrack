using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RondiTrack.Telemetry;

namespace RondiTrack.Auth;

public interface IStokvelAccess
{
    Task EnsureAsync(ClaimsPrincipal user, Guid stokvelId, string policy, Guid? onBehalfOfUserId = null);
}

// One place that decides 404 vs 403. No try/catch, no role names.
public sealed class StokvelAccess(IAuthorizationService authorization, ILogger<StokvelAccess> logger) : IStokvelAccess   // NEW 6.2: logger
{
    public async Task EnsureAsync(ClaimsPrincipal user, Guid stokvelId, string policy, Guid? onBehalfOfUserId = null)
    {
        var result = await authorization.AuthorizeAsync(user, new StokvelResource(stokvelId, onBehalfOfUserId), policy);
        if (result.Succeeded) return;

        var notMember = result.Failure?.FailureReasons
            .Any(r => r.Message == StokvelAccessHandler.NotMemberReason) ?? false;

        // not a member: pretend the stokvel does not exist (same text as the real 404)
        if (notMember)
        {
            RondiMetrics.AuthorizationRefused(RefusalResults.NotFound);   // NEW 6.2: count it
            // NEW 6.2: Information. A refusal is an expected answer; a spike is caught by the metric. Our user id only.
            logger.LogInformation("Stokvel access refused ({Result}) for user {UserId}",
                RefusalResults.NotFound, user.GetUserId());
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        }

        // a member without the role already knows the stokvel exists
        RondiMetrics.AuthorizationRefused(RefusalResults.Forbidden);      // NEW 6.2: count it
        logger.LogInformation("Stokvel access refused ({Result}) for user {UserId}",
            RefusalResults.Forbidden, user.GetUserId());
        throw new ForbiddenException("You do not have the required role in this stokvel.");
    }
}
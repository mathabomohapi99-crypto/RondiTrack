using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Auth;

// What level of access the endpoint needs
public enum StokvelAccessLevel
{
    Member,              // any member
    Officer,             // Treasurer or Admin of THAT stokvel
    RecordContribution   // own contribution, or officer for someone else's
}

// The custom requirement
public sealed record StokvelAccessRequirement(StokvelAccessLevel Level) : IAuthorizationRequirement;

// The resource being protected: one stokvel (and optionally who the action is for)
public sealed record StokvelResource(Guid StokvelId, Guid? OnBehalfOfUserId = null);

// Resource-based handler. It asks the DATABASE every time, so a removed or demoted member is refused at once.
public sealed class StokvelAccessHandler(IStokvelMemberRepository members)
    : AuthorizationHandler<StokvelAccessRequirement, StokvelResource>
{
    public const string NotMemberReason = "not-a-member";     // becomes 404
    public const string MissingRoleReason = "missing-role";   // becomes 403

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, StokvelAccessRequirement requirement, StokvelResource resource)
    {
        // platform admins (support staff) pass on every stokvel
        if (context.User.IsPlatformAdmin())
        {
            context.Succeed(requirement);
            return;
        }

        var callerId = context.User.GetUserId();
        if (callerId is null) return;   // not signed in: no success

        // the real check: the caller's membership row for THIS stokvel
        var membership = await members.GetAsync(callerId.Value, resource.StokvelId);
        if (membership is null)
        {
            context.Fail(new AuthorizationFailureReason(this, NotMemberReason));
            return;
        }

        var isOfficer = membership.Role is StokvelRole.Treasurer or StokvelRole.Admin;

        var needsOfficer = requirement.Level switch
        {
            StokvelAccessLevel.Officer => true,
            // recording for yourself is fine; for someone else you must be an officer
            StokvelAccessLevel.RecordContribution =>
                resource.OnBehalfOfUserId is Guid target && target != callerId.Value,
            _ => false
        };

        if (needsOfficer && !isOfficer)
        {
            context.Fail(new AuthorizationFailureReason(this, MissingRoleReason));
            return;
        }

        context.Succeed(requirement);
    }
}
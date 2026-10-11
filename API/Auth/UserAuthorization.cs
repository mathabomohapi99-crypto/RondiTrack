using Microsoft.AspNetCore.Authorization;

namespace RondiTrack.Auth;

// "You may only touch your own account" (platform admins excepted)
public sealed record UserSelfRequirement : IAuthorizationRequirement;

public sealed record UserResource(Guid UserId);

public sealed class UserSelfHandler : AuthorizationHandler<UserSelfRequirement, UserResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UserSelfRequirement requirement, UserResource resource)
    {
        // own account, or platform admin
        if (context.User.IsPlatformAdmin() || context.User.GetUserId() == resource.UserId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
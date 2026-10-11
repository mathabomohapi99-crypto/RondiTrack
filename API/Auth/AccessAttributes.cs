using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RondiTrack.Auth;

// Base: runs BEFORE the action (before model binding), reads {stokvelId} from the route
public abstract class StokvelAccessFilterBase(string policy) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // fail closed: no usable stokvel id means no access
        if (!Guid.TryParse(context.RouteData.Values["stokvelId"]?.ToString(), out var stokvelId))
            throw new DomainNotFoundException("Stokvel was not found.");

        var access = context.HttpContext.RequestServices.GetRequiredService<IStokvelAccess>();
        await access.EnsureAsync(context.HttpContext.User, stokvelId, policy);
    }
}

// Use on an endpoint: any member of the stokvel
public sealed class RequireStokvelMemberAttribute() : StokvelAccessFilterBase(Policies.StokvelMember);

// Use on an endpoint: Treasurer or Admin of the stokvel
public sealed class RequireStokvelOfficerAttribute() : StokvelAccessFilterBase(Policies.StokvelOfficer);

// Use on /api/users/{id} endpoints: your own account only
public sealed class RequireUserSelfAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var authorization = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();

        // an unreadable id becomes Guid.Empty, which matches nobody (fails closed)
        Guid.TryParse(context.RouteData.Values["id"]?.ToString(), out var userId);

        var result = await authorization.AuthorizeAsync(context.HttpContext.User, new UserResource(userId), Policies.UserSelf);
        if (!result.Succeeded)
            throw new ForbiddenException("You can only access your own account.");   // 403 even if the id does not exist
    }
}
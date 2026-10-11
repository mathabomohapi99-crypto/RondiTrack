using System.Security.Claims;

namespace RondiTrack.Auth;

public static class UserClaimsExtensions
{
    // the signed-in user's id from the "sub" claim (null if there is none)
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;

    // platform admin = has the platform role claim
    public static bool IsPlatformAdmin(this ClaimsPrincipal user) =>
        user.HasClaim("role", AuthConstants.PlatformAdminRole);
}
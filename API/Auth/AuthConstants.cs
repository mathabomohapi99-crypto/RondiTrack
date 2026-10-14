using RondiTrack.Common;
namespace RondiTrack.Auth;

public static class AuthConstants
{
    public const string PlatformAdminRole = "PlatformAdmin";   // the ONLY role in the token (platform level)
    public const string RefreshCookieName = "rondi_refresh";   // name of the refresh cookie
    public const string RefreshCookiePath = ApiRoutes.V1Prefix + "/auth";       // cookie is only sent to the auth endpoints
}

// Policy names live here, so controllers never contain role strings
public static class Policies
{
    public const string StokvelMember = "StokvelMember";           // any member of the stokvel
    public const string StokvelOfficer = "StokvelOfficer";         // Treasurer or Admin of the stokvel
    public const string RecordContribution = "RecordContribution"; // own contribution, or officer for others
    public const string UserSelf = "UserSelf";                     // your own account (or platform admin)
    public const string PlatformAdmin = "PlatformAdmin";           // platform admin claim
}
namespace RondiTrack.Auth;

// Settings for tokens. Everything except SigningKey comes from appsettings.json.
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "RondiTrack";
    public string Audience { get; set; } = "RondiTrack.Api";
    public string SigningKey { get; set; } = "";      // comes ONLY from user-secrets (never appsettings)
    public int AccessTokenMinutes { get; set; } = 10; // short on purpose
    public int RefreshTokenDays { get; set; } = 7;
}
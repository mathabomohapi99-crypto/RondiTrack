using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace RondiTrack.Auth;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, bool isPlatformAdmin);
    (string Raw, string Hash) CreateRefreshToken();
    string HashRefreshToken(string raw);
}

public sealed class TokenService(JwtOptions options) : ITokenService
{
    // the signing key as bytes (HS256 needs at least 32)
    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(options.SigningKey));

    public AccessToken CreateAccessToken(Guid userId, bool isPlatformAdmin)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);

        // Only facts that can safely be stale for a few minutes. NO stokvel roles here.
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),          // who the user is (never changes)
            new("jti", Guid.NewGuid().ToString())   // unique id of this token
        };
        if (isPlatformAdmin)
            claims.Add(new Claim("role", AuthConstants.PlatformAdminRole));   // platform-level only

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }

    // Random bytes, NOT a JWT. The raw value goes to the client; only its hash is stored.
    public (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (raw, HashRefreshToken(raw));
    }

    // SHA-256 is enough here because the token is 256 bits of randomness (not a guessable password)
    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
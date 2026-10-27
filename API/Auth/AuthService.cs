using Microsoft.AspNetCore.Identity;
using RondiTrack.Dtos;

namespace RondiTrack.Auth;

// What the controller needs after login or refresh
public sealed record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,              // goes ONLY into the cookie
    DateTime RefreshTokenExpiresAtUtc,
    Guid RefreshTokenId);

public interface IAuthService
{
    Task<User> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RefreshAsync(string? rawRefreshToken);
    Task LogoutAsync(string? rawRefreshToken);
}

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IPasswordHasher<User> hasher,
    ITokenService tokenService,
    JwtOptions jwt,
    ILogger<AuthService> logger) : IAuthService
{
    // A fake user and hash. Used when the email is unknown, so the time taken looks the same as a wrong password.
    private static readonly User DummyUser = new("Dummy", "dummy@example.com");
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(DummyUser, "dummy-password-never-used");

    public async Task<User> RegisterAsync(RegisterRequest request)
    {
        var user = new User(request.FullName, request.Email);   // trims + lower-cases the email

        if (await users.EmailExistsAsync(user.Email))
            throw new DomainConflictException("An account with this email already exists.");   // 409

        user.SetPasswordHash(hasher.HashPassword(user, request.Password));   // store the hash only
        await users.AddAsync(user);   // the unique index also catches two requests at the same moment

        logger.LogInformation("User {UserId} registered", user.Id);   // id only, never email or password
        return user;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await users.GetByEmailAsync(request.Email);

        // always run one password check (real or fake hash) so unknown email and wrong password look alike
        var hash = user?.PasswordHash ?? DummyHash;
        var outcome = hasher.VerifyHashedPassword(user ?? DummyUser, hash, request.Password);

        if (user is null || user.PasswordHash is null || outcome == PasswordVerificationResult.Failed)
        {
            // CHANGED 6.2: never the typed email. Our own user id when the account exists, so a guessing attack on one account is visible.
            if (user is null)
                logger.LogWarning("Login failed: no matching account");
            else
                logger.LogWarning("Login failed for user {UserId}", user.Id);
            throw new InvalidCredentialsException();   // same 401 message for both cases
        }

        var result = await IssueAsync(user, Guid.NewGuid());   // new login = new token family
        logger.LogInformation("Login succeeded for user {UserId}", user.Id);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw new InvalidRefreshTokenException();

        var now = DateTime.UtcNow;
        var stored = await tokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken))
            ?? throw new InvalidRefreshTokenException();

        // REUSE DETECTION: this token was already used (or revoked) -> someone replayed it
        if (stored.UsedAtUtc.HasValue || stored.RevokedAtUtc.HasValue)
        {
            await tokens.RevokeFamilyAsync(stored.FamilyId, now);   // kill every token of this login
            logger.LogWarning("Refresh token reuse detected for user {UserId}. Session {FamilyId} revoked",
                stored.UserId, stored.FamilyId);
            throw new InvalidRefreshTokenException();
        }

        if (stored.ExpiresAtUtc <= now)
            throw new InvalidRefreshTokenException();

        // read the user again, so the new access token reflects their CURRENT platform flag
        var user = await users.GetByIdAsync(stored.UserId)
            ?? throw new InvalidRefreshTokenException();

        var result = await IssueAsync(user, stored.FamilyId);   // ROTATION: new token, same family

        // mark the old one used. If another request got there first, this is a replay.
        if (!await tokens.TryMarkUsedAsync(stored.Id, result.RefreshTokenId, now))
        {
            await tokens.RevokeFamilyAsync(stored.FamilyId, now);
            logger.LogWarning("Refresh token reuse detected for user {UserId}. Session {FamilyId} revoked",
                stored.UserId, stored.FamilyId);
            throw new InvalidRefreshTokenException();
        }

        logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
        return result;
    }

    // Idempotent: unknown or missing token is fine, it just does nothing
    public async Task LogoutAsync(string? rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;

        var stored = await tokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken));
        if (stored is null) return;

        await tokens.RevokeFamilyAsync(stored.FamilyId, DateTime.UtcNow);
        logger.LogInformation("User {UserId} logged out", stored.UserId);
    }

    // Makes the access token + a new refresh token (stored as a hash) for one family
    private async Task<AuthResult> IssueAsync(User user, Guid familyId)
    {
        var now = DateTime.UtcNow;
        var access = tokenService.CreateAccessToken(user.Id, user.IsPlatformAdmin);
        var (raw, hash) = tokenService.CreateRefreshToken();

        var refresh = new RefreshToken(user.Id, familyId, hash, now, now.AddDays(jwt.RefreshTokenDays));
        await tokens.AddAsync(refresh);

        return new AuthResult(access.Value, access.ExpiresAtUtc, raw, refresh.ExpiresAtUtc, refresh.Id);
    }
}
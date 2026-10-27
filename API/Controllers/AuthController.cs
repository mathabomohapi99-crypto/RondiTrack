using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Web;
using RondiTrack.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Auth;
using RondiTrack.Dtos;

namespace RondiTrack.Controllers;

[Route($"{ApiRoutes.V1}/auth")]
[AllowAnonymous]   // all four endpoints work without an access token
public class AuthController(IAuthService auth) : RondiControllerBase
{
    /// <summary>Registers a new account. Password must be at least 12 characters.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var user = await auth.RegisterAsync(request);
        return Created($"{ApiRoutes.V1Prefix}/users/{user.Id}", user.ToResponse());
    }

    /// <summary>Signs in. Returns the access token in the body and sets the refresh token as a cookie.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        TokenResponse(await auth.LoginAsync(request));

    /// <summary>Swaps the refresh cookie for a new access token and a new refresh cookie (rotation).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        Request.Cookies.TryGetValue(AuthConstants.RefreshCookieName, out var raw);   // cookie is the only source
        return TokenResponse(await auth.RefreshAsync(raw));
    }

    /// <summary>Signs out: revokes the session's refresh tokens. Safe to call more than once.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        Request.Cookies.TryGetValue(AuthConstants.RefreshCookieName, out var raw);
        await auth.LogoutAsync(raw);   // does nothing if there is no valid cookie
        Response.Cookies.Delete(AuthConstants.RefreshCookieName, CookieOptionsFor(null));   // same path as when it was set
        return NoContent();
    }

    // Sets the refresh cookie and returns ONLY the access token in the body
    private ActionResult<AuthResponse> TokenResponse(AuthResult result)
    {
        Response.Cookies.Append(AuthConstants.RefreshCookieName, result.RefreshToken,
            CookieOptionsFor(result.RefreshTokenExpiresAtUtc));
        Response.Headers.CacheControl = "no-store";   // tokens must never be cached
        return Ok(new AuthResponse(result.AccessToken, result.AccessTokenExpiresAtUtc, "Bearer"));
    }

    // The cookie flags (each one is justified in the README)
    private static CookieOptions CookieOptionsFor(DateTime? expiresUtc) => new()
    {
        HttpOnly = true,                              // JavaScript cannot read it, so XSS cannot steal it
        Secure = true,                                // only sent over HTTPS
        SameSite = SameSiteMode.Strict,               // never sent on cross-site requests (CSRF)
        Path = AuthConstants.RefreshCookiePath,       // only sent to /api/auth/*
        IsEssential = true,                           // not blocked by consent middleware
        Expires = expiresUtc.HasValue ? new DateTimeOffset(expiresUtc.Value) : null   // lives as long as the token
    };
}
namespace RondiTrack.Dtos;

public sealed record RegisterRequest(string FullName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

// The refresh token is NOT here on purpose: it only travels in a cookie
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string TokenType);
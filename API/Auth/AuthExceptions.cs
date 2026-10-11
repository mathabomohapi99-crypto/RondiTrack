namespace RondiTrack.Auth;

// Same message for unknown email AND wrong password, so nobody can learn which emails have accounts
public sealed class InvalidCredentialsException() : Exception("Invalid email or password.");

// Used for a missing, unknown, used, revoked or expired refresh token
public sealed class InvalidRefreshTokenException() : Exception("The refresh token is invalid or has expired.");

// Becomes a 403 problem response
public sealed class ForbiddenException(string message) : Exception(message);
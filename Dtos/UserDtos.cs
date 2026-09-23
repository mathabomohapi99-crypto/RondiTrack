namespace RondiTrack.Dtos;

public sealed record UserRequest(string FullName, string Email);

public sealed record UserResponse(Guid Id, string FullName, string Email, DateTime CreatedAtUtc);
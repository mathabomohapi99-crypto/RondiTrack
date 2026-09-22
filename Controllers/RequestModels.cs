using RondiTrack.Domain;

namespace RondiTrack.Controllers;

public sealed record UserRequest(string FullName, string Email);

public sealed record StokvelRequest(
    string Name,
    decimal ContributionAmount,
    ContributionFrequency Frequency,
    int MaxMembers);

public sealed record AddMemberRequest(Guid UserId);
using RondiTrack.Domain;

namespace RondiTrack.Dtos;

public static class MappingExtensions
{
    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.FullName, user.Email, user.CreatedAtUtc);

    public static StokvelResponse ToResponse(this Stokvel stokvel) =>
        new(stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.Frequency,
            stokvel.MaxMembers, stokvel.Members.Count, stokvel.IsFull, stokvel.PayoutPerCycle,
            stokvel.CreatedAtUtc);

    public static ContributionResponse ToResponse(this Contribution contribution) =>
        new(contribution.Id, contribution.StokvelId, contribution.UserId,
            contribution.CycleId, contribution.Amount, contribution.RecordedAtUtc);

    public static ContributionCycleResponse ToResponse(this ContributionCycle cycle) =>
        new(cycle.Id, cycle.StokvelId, cycle.CycleNumber, cycle.TargetAmount, cycle.CreatedAtUtc);
}
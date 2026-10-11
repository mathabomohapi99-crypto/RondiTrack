using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

public sealed class ContributionRequestValidator : AbstractValidator<ContributionRequest>
{
    public ContributionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
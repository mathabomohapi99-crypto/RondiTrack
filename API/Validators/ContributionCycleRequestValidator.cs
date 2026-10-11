using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

public sealed class ContributionCycleRequestValidator : AbstractValidator<ContributionCycleRequest>
{
    public ContributionCycleRequestValidator()
    {
        RuleFor(x => x.CycleNumber).GreaterThan(0);
        RuleFor(x => x.TargetAmount).GreaterThan(0);
    }
}
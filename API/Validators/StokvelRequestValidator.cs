using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

public sealed class StokvelRequestValidator : AbstractValidator<StokvelRequest>
{
    public StokvelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ContributionAmount).GreaterThan(0);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.MaxMembers).InclusiveBetween(2, 50);
    }
}
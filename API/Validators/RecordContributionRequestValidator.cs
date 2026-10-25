using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

// NEW 7.4: validates the cents endpoint. Errors come back as { errors: { amountInCents: [...] } } (camelCase).
public sealed class RecordContributionRequestValidator : AbstractValidator<RecordContributionRequest>
{
    public RecordContributionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AmountInCents).GreaterThan(0);
    }
}
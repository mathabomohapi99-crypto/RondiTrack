using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
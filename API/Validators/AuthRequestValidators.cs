using FluentValidation;
using RondiTrack.Dtos;

namespace RondiTrack.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(12).WithMessage("Password must be at least 12 characters.")        // required rule
            .MaximumLength(128).WithMessage("Password cannot be longer than 128 characters.");  // stops huge inputs being hashed
    }
}

// Login only checks the fields are present. It never explains password rules.
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
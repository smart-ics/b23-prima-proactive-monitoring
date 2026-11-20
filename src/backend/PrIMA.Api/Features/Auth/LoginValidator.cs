using FluentValidation;

namespace PrIMA.Api.Features.Auth;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .Must(u => !string.IsNullOrWhiteSpace(u)).WithMessage("Username cannot be whitespace only.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

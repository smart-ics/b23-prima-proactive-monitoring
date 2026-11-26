using FluentValidation;

namespace PrIMA.Api.Features.Detectors.Enrollment.GenerateEnrollmentToken;

public sealed class GenerateEnrollmentTokenValidator : AbstractValidator<GenerateEnrollmentTokenRequest>
{
    public GenerateEnrollmentTokenValidator()
    {
        RuleFor(x => x.Label)
            .MaximumLength(100)
            .WithMessage("Enrollment token label must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Label));
    }
}

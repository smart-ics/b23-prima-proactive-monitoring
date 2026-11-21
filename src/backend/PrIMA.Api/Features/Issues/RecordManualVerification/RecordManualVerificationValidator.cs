using FluentValidation;

namespace PrIMA.Api.Features.Issues.RecordManualVerification;

public sealed class RecordManualVerificationValidator : AbstractValidator<RecordManualVerificationRequest>
{
    public RecordManualVerificationValidator()
    {
        RuleFor(x => x.EvidenceNotes)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Evidence notes cannot be empty.")
            .Must(notes => !string.IsNullOrWhiteSpace(notes)).WithMessage("Evidence notes cannot be whitespace.");
    }
}

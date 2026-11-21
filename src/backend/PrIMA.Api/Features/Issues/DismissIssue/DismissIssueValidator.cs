using FluentValidation;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.DismissIssue;

public sealed class DismissIssueValidator : AbstractValidator<DismissIssueRequest>
{
    public DismissIssueValidator()
    {
        RuleFor(x => x.DismissalReason)
            .NotEmpty().WithMessage("Dismissal reason is required.")
            .IsEnumName(typeof(DismissalReason), caseSensitive: false)
            .WithMessage("Dismissal reason must be one of: FalsePositive, AcceptedOperationalException, DuplicateManualReport.");

        RuleFor(x => x.JustificationRationale)
            .NotEmpty().WithMessage("Justification rationale is required.")
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 20)
            .WithMessage("Dismissal justification rationale must be at least 20 characters.");
    }
}

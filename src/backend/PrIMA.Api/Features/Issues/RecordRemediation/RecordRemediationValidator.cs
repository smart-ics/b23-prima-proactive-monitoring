using FluentValidation;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Issues.RecordRemediation;

public sealed class RecordRemediationValidator : AbstractValidator<RecordRemediationRequest>
{
    public RecordRemediationValidator()
    {
        RuleFor(x => x.ActionCategory)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Action category is required.")
            .IsEnumName(typeof(RemediationActionCategory), caseSensitive: false)
            .WithMessage("Action category must be one of: DataCorrection, ServiceRestart, ConfigurationAdjustment, CodeRemediationRequested.");

        RuleFor(x => x.SummaryDescription)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Summary description cannot be empty.")
            .Must(desc => !string.IsNullOrWhiteSpace(desc)).WithMessage("Summary description cannot be whitespace.");
    }
}

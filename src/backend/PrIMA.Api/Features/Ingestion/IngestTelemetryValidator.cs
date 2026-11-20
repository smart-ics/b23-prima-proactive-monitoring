using FluentValidation;
using PrIMA.Api.Domain.Detectors;
using PrIMA.Api.Domain.Issues;

namespace PrIMA.Api.Features.Ingestion;

public sealed class IngestTelemetryValidator : AbstractValidator<IngestTelemetryRequest>
{
    public IngestTelemetryValidator()
    {
        RuleFor(x => x.DetectorId)
            .NotEmpty()
            .WithMessage("Detector ID is required.");

        RuleFor(x => x.ExecutionTimestampUtc)
            .NotEmpty()
            .WithMessage("Execution timestamp is required.")
            .Must(ts => ts >= DateTime.UtcNow.AddHours(-24) && ts <= DateTime.UtcNow.AddHours(24))
            .WithMessage("Execution timestamp must be within 24 hours of current UTC time.");

        RuleFor(x => x.TriggerSource)
            .NotEmpty()
            .WithMessage("Trigger source is required.")
            .Must(s => s is "Scheduled" or "Manual" or "EventHook")
            .WithMessage("Trigger source must be one of: Scheduled, Manual, EventHook.");

        RuleFor(x => x.Outcome)
            .NotEmpty()
            .WithMessage("Outcome is required.")
            .Must(o => o is "Passed" or "FindingDiscovered" or "ExecutionFailed")
            .WithMessage("Outcome must be one of: Passed, FindingDiscovered, ExecutionFailed.")
            .Must(o => o != "ExecutionFailed")
            .WithMessage("ExecutionFailed outcome is supported in Stage 3 with infrastructure anomaly synthesis.");

        RuleFor(x => x.DurationMs)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Duration must be non-negative.");

        When(x => x.Outcome == "Passed", () =>
        {
            RuleFor(x => x.Findings)
                .Must(f => f == null || f.Count == 0)
                .WithMessage("Findings must be null or empty when outcome is 'Passed'.");
        });

        When(x => x.Outcome == "FindingDiscovered", () =>
        {
            RuleFor(x => x.Findings)
                .NotNull()
                .WithMessage("Findings are required when outcome is 'FindingDiscovered'.")
                .Must(f => f != null && f.Count >= 1)
                .WithMessage("Findings must contain at least one item when outcome is 'FindingDiscovered'.");
        });

        RuleFor(x => x.Findings)
            .Must(findings =>
            {
                if (findings == null || findings.Count <= 1) return true;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var finding in findings)
                {
                    if (string.IsNullOrWhiteSpace(finding?.TargetEntityReference)) continue;
                    var trimmed = finding.TargetEntityReference.Trim();
                    if (!seen.Add(trimmed))
                    {
                        return false;
                    }
                }
                return true;
            })
            .WithMessage("Payload contains duplicate targetEntityReference findings within the same batch.");

        When(x => x.Findings != null, () =>
        {
            RuleForEach(x => x.Findings).ChildRules(finding =>
            {
                finding.RuleFor(f => f.TargetEntityReference)
                    .NotEmpty()
                    .WithMessage("Target entity reference is required.")
                    .Must(r => r != null && r.Trim().Length <= 213)
                    .WithMessage("Target entity reference trimmed length cannot exceed 213 characters.");

                finding.RuleFor(f => f.Title)
                    .NotEmpty()
                    .WithMessage("Title is required.")
                    .MaximumLength(250)
                    .WithMessage("Title length cannot exceed 250 characters.");

                finding.RuleFor(f => f.BaseSeverity)
                    .NotEmpty()
                    .WithMessage("Base severity is required.")
                    .Must(s => Enum.TryParse<IssueSeverity>(s, true, out _))
                    .WithMessage("Base severity must be one of: Low, Medium, High, Critical.");

                finding.RuleFor(f => f.Evidence)
                    .NotNull()
                    .WithMessage("Evidence is required.");

                finding.When(f => f.Evidence != null, () =>
                {
                    finding.RuleFor(f => f.Evidence.Module)
                        .NotEmpty()
                        .WithMessage("Evidence module is required.")
                        .MaximumLength(100)
                        .WithMessage("Evidence module cannot exceed 100 characters.");

                    finding.RuleFor(f => f.Evidence.SourceTable)
                        .MaximumLength(100)
                        .When(f => f.Evidence.SourceTable != null)
                        .WithMessage("Evidence source table cannot exceed 100 characters.");

                    finding.RuleFor(f => f.Evidence.RecordKeys)
                        .MaximumLength(500)
                        .When(f => f.Evidence.RecordKeys != null)
                        .WithMessage("Evidence record keys cannot exceed 500 characters.");
                });
            });
        });
    }
}

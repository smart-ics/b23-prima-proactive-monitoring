using FluentValidation;

namespace PrIMA.Api.Features.Detectors.ToggleDetectorStatus;

public sealed class ToggleDetectorStatusValidator : AbstractValidator<ToggleDetectorStatusRequest>
{
    private static readonly string[] AllowedActions = ["Pause", "Resume", "Retire"];

    public ToggleDetectorStatusValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .Must(a => AllowedActions.Contains(a?.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Action must be one of: 'Pause', 'Resume', 'Retire'.");
    }
}

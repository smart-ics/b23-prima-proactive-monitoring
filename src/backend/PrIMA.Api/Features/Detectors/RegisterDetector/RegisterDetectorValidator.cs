using FluentValidation;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.RegisterDetector;

public sealed class RegisterDetectorValidator : AbstractValidator<RegisterDetectorRequest>
{
    public RegisterDetectorValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Detector name is required.")
            .MaximumLength(100).WithMessage("Detector name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Detector description is required.")
            .MaximumLength(500).WithMessage("Detector description cannot exceed 500 characters.");

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Monitoring scope is required.")
            .IsEnumName(typeof(MonitoringScope), caseSensitive: false)
            .WithMessage("Monitoring scope must be one of: DataQuality, BusinessLogic, Infrastructure, ProcessFlow, DataIntegrity, ProcessIntegrity, DataSync.");

        RuleFor(x => x.TargetModule)
            .NotEmpty().WithMessage("Target module is required.")
            .MaximumLength(100).WithMessage("Target module cannot exceed 100 characters.");

        RuleFor(x => x.ExpectedCadenceSeconds)
            .GreaterThan(0).WithMessage("Expected cadence must be greater than zero seconds.");

        RuleFor(x => x.WebhookUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("WebhookUrl must be a valid absolute HTTP or HTTPS URL if specified.");
    }
}

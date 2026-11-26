using FluentValidation;
using PrIMA.Api.Domain.Detectors;

namespace PrIMA.Api.Features.Detectors.Enrollment.DetectorHandshake;

public sealed class DetectorHandshakeValidator : AbstractValidator<DetectorHandshakeRequest>
{
    private static readonly HashSet<string> CanonicalScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(MonitoringScope.DataIntegrity),
        nameof(MonitoringScope.ProcessIntegrity),
        nameof(MonitoringScope.DataSync),
        nameof(MonitoringScope.Infrastructure)
    };

    public DetectorHandshakeValidator()
    {
        RuleFor(x => x.EnrollmentToken)
            .NotEmpty().WithMessage("Enrollment token is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Detector name is required.")
            .MaximumLength(150).WithMessage("Detector name cannot exceed 150 characters.");

        RuleFor(x => x.TargetModule)
            .NotEmpty().WithMessage("Target module is required.")
            .MaximumLength(100).WithMessage("Target module cannot exceed 100 characters.");

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Monitoring scope is required.")
            .Must(scope => !string.IsNullOrWhiteSpace(scope) && CanonicalScopes.Contains(scope.Trim()))
            .WithMessage("Monitoring scope must be one of: DataIntegrity, ProcessIntegrity, DataSync, Infrastructure.");

        RuleFor(x => x.ExpectedCadenceSeconds)
            .InclusiveBetween(30, 86400)
            .WithMessage("Expected cadence must be between 30 and 86400 seconds.");

        RuleFor(x => x.WebhookUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("WebhookUrl must be a valid absolute HTTP or HTTPS URL if specified.")
            .MaximumLength(500).WithMessage("WebhookUrl cannot exceed 500 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.Version)
            .MaximumLength(50).WithMessage("Version cannot exceed 50 characters.");
    }
}

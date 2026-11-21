using FluentValidation;

namespace PrIMA.Api.Features.History.GetReliabilityKpi;

public sealed class GetReliabilityKpiValidator : AbstractValidator<GetReliabilityKpiQuery>
{
    public GetReliabilityKpiValidator()
    {
        RuleFor(x => x.FromUtc)
            .Must((query, fromUtc) => NormalizeToUtc(fromUtc) <= NormalizeToUtc(query.ToUtc))
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }

    internal static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

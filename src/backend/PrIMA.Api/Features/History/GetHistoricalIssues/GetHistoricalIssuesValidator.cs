using FluentValidation;

namespace PrIMA.Api.Features.History.GetHistoricalIssues;

public sealed class GetHistoricalIssuesValidator : AbstractValidator<GetHistoricalIssuesQuery>
{
    public GetHistoricalIssuesValidator()
    {
        RuleFor(x => x.FromUtc)
            .Must((query, fromUtc) => NormalizeToUtc(fromUtc) <= NormalizeToUtc(query.ToUtc))
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }

    internal static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value
    };
}

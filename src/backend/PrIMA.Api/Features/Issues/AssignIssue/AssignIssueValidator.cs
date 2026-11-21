using FluentValidation;

namespace PrIMA.Api.Features.Issues.AssignIssue;

public sealed class AssignIssueValidator : AbstractValidator<AssignIssueRequest>
{
    public AssignIssueValidator()
    {
        RuleFor(x => x.OfficerId)
            .NotEmpty()
            .WithMessage("OfficerId must be provided.");
    }
}

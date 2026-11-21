using FluentValidation;

namespace PrIMA.Api.Features.Issues.AddNote;

public sealed class AddNoteValidator : AbstractValidator<AddNoteRequest>
{
    public AddNoteValidator()
    {
        RuleFor(x => x.NoteText)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Note text cannot be empty.")
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("Note text cannot be whitespace.");
    }
}

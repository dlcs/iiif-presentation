using FluentValidation;
using Models.API.Manifest;

namespace API.Features.Manifest.Validators;

public class AdjunctValidator : AbstractValidator<Adjunct>
{
    private static readonly char[] ProhibitedCharacters = ['/', '=', ','];
    private static readonly string ProhibitedCharacterDisplay =
        string.Join(", ", ProhibitedCharacters.Select(p => $"'{p}'"));

    public AdjunctValidator()
    {
        RuleFor(a => a.Id)
            .NotEmpty()
            .WithMessage("Adjunct 'id' must not be empty");

        RuleFor(a => a.Id)
            .Must(id => !ProhibitedCharacters.Any(id.Contains))
            .When(a => !string.IsNullOrEmpty(a.Id))
            .WithMessage($"Adjunct 'id' contains a prohibited character. Cannot contain any of: {ProhibitedCharacterDisplay}");

        RuleFor(a => a)
            .Must(a => a.ExternalId != null || !string.IsNullOrEmpty(a.Origin))
            .WithMessage("Adjunct must have either 'externalId' or 'origin' set");
    }
}

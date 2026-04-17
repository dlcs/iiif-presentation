using Core.Helpers;
using FluentValidation;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Features.Manifest.Validators;

public class AdjunctValidator : AbstractValidator<JObject>
{
    public AdjunctValidator()
    {
        RuleFor(a => a[AssetProperties.Id])
            .Must(id => id is { Type: JTokenType.String } && !string.IsNullOrEmpty(id.Value<string>()))
            .WithMessage("Adjunct 'id' must not be empty");

        RuleFor(a => a[AssetProperties.Id])
            .Must(id => !ProhibitedCharacters.Characters.Any(id!.Value<string>()!.Contains))
            .When(a => a.ContainsKey(AdjunctProperties.Id) &&
                       a[AdjunctProperties.Id] is { Type: JTokenType.String } t &&
                       !string.IsNullOrEmpty(t.Value<string>()))
            .WithMessage($"Adjunct 'id' contains a prohibited character. Cannot contain any of: {ProhibitedCharacters.Display}");

        RuleFor(a => a)
            .Must(a => a.ContainsKey(AdjunctProperties.ExternalId) || a.ContainsKey(AdjunctProperties.Origin))
            .WithMessage("Adjunct must have either 'externalId' or 'origin' set");
    }
}

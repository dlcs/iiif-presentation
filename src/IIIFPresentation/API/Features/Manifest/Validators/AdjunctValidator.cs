using FluentValidation;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Services.Manifests.Settings;

namespace API.Features.Manifest.Validators;

public class AdjunctValidator : AbstractValidator<JObject>
{
    public AdjunctValidator(ServicesSettings settings)
    {
        RuleFor(a => a[AdjunctProperties.Id])
            .Must(id => id is { Type: JTokenType.String } && !string.IsNullOrEmpty(id.Value<string>()))
            .WithMessage("Adjunct 'id' must not be empty");

        RuleFor(a => a[AdjunctProperties.Id])
            .Must(id => !settings.ProhibitedCharacters.Any(id!.Value<string>()!.Contains))
            .When(a => a.ContainsKey(AdjunctProperties.Id) &&
                       a[AdjunctProperties.Id] is { Type: JTokenType.String } t &&
                       !string.IsNullOrEmpty(t.Value<string>()))
            .WithMessage($"Adjunct 'id' contains a prohibited character. Cannot contain any of: {settings.ProhibitedCharactersDisplay}");
    }
}

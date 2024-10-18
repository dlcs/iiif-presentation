using FluentValidation;
using Models.API.Manifest;

namespace API.Features.Manifest.Validators;

public class PresentationManifestValidator : AbstractValidator<PresentationManifest>
{
    public PresentationManifestValidator()
    {
        RuleFor(f => f.Parent).NotEmpty().WithMessage("Requires a 'parent' to be set");
        RuleFor(f => f.Slug).NotEmpty().WithMessage("Requires a 'slug' to be set");
    }
}
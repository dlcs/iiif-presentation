using API.Infrastructure.Validation;
using Core.Helpers;
using FluentValidation;
using Models.API.Manifest;

namespace API.Features.Manifest.Validators;

public class PresentationManifestValidator : AbstractValidator<PresentationManifest>
{
    public PresentationManifestValidator()
    {
        RuleFor(f => f.Parent).NotEmpty().WithMessage("Requires a 'parent' to be set");
        RuleFor(f => f.Slug).NotEmpty().WithMessage("Requires a 'slug' to be set")
            .Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
            .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");
        RuleFor(f => f.Items).Empty()
            .When(f => !f.PaintedResources.IsNullOrEmpty())
            .WithMessage("The properties \"items\" and \"paintedResource\" cannot be used at the same time");
        
    }
}

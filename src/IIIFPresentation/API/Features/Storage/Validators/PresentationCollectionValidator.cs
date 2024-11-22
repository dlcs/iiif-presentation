using API.Infrastructure.Validation;
using FluentValidation;
using Models.API.Collection;

namespace API.Features.Storage.Validators;

public class PresentationCollectionValidator : AbstractValidator<PresentationCollection>
{
    public PresentationCollectionValidator()
    {
        RuleFor(f => f.Parent).NotEmpty().WithMessage("Requires a 'parent' to be set");
        RuleFor(f => f.Slug).NotEmpty().WithMessage("Requires a 'slug' to be set")
            .Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
            .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");
    }
}
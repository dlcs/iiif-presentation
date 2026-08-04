using API.Infrastructure.Validation;
using FluentValidation;
using Models.API;
using Models.API.General;

namespace API.Features.Storage.Validators;

public class PresentationValidator : AbstractValidator<IPresentation>
{
    public PresentationValidator()
    {
        RuleFor(f => f.Parent).Must(p => Uri.IsWellFormedUriString(p, UriKind.Absolute))
            .When(f => f.Parent != null)
            .WithMessage("'parent' must be a well formed URI");

        RuleFor(f => f.Parent).NotEmpty()
            .When(f => f.PublicId == null).WithMessage("Requires a 'parent' to be set");

        RuleFor(f => f.Slug).NotEmpty()
            .When(f => f.PublicId == null)
            .WithMessage("Requires a 'slug' to be set")
            .Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
            .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");

        RuleFor(f => f.Slug)
            .Must(slug => !slug!.Contains('/'))
            .WithMessage("'slug' cannot contain '/'")
            .Must(slug => !Uri.IsWellFormedUriString(slug, UriKind.Absolute))
            .WithMessage("'slug' cannot be a fully qualified URI")
            .When(f => !string.IsNullOrEmpty(f.Slug));

        RuleFor(f => f.PublicId)
            .NotEmpty()
            .When(f => f.Parent == null && f.Slug == null)
            .WithMessage("'public ID' is required if the 'slug' and 'parent' are not specified");
    }
}

using API.Settings;
using FluentValidation;
using Microsoft.Extensions.Options;
using Models.API;
using Models.API.General;

namespace API.Features.Storage.Validators;

public class PresentationValidator : AbstractValidator<IPresentation>
{
    /// <param name="isFlatRequest">
    /// Flat requests always require a 'parent'/'slug' or 'publicId' to be present in the body, since the URL alone
    /// can't identify where the resource lives. Hierarchical requests can legitimately omit these from the body -
    /// the URL supplies what's needed - so hierarchical callers construct this with <c>false</c>.
    /// </param>
    public PresentationValidator(IOptions<ApiSettings> apiOptions, bool isFlatRequest = true)
    {
        var settings = apiOptions.Value;

        RuleFor(f => f.Parent).Must(p => Uri.IsWellFormedUriString(p, UriKind.Absolute))
            .When(f => f.Parent != null)
            .WithMessage("'parent' must be a well formed URI");

        RuleFor(f => f.Parent).NotEmpty()
            .When(f => isFlatRequest && f.PublicId == null).WithMessage("Requires a 'parent' to be set");

        RuleFor(f => f.Slug).NotEmpty()
            .When(f => isFlatRequest && f.PublicId == null)
            .WithMessage("Requires a 'slug' to be set");

        RuleFor(f => f.Slug).Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
            .When(f => f.Slug != null)
            .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");

        RuleFor(f => f.Slug)
            .Must(slug => !settings.ProhibitedSlugCharacters.Any(slug!.Contains))
            .WithMessage($"'slug' contains a prohibited character. Cannot contain any of: {settings.ProhibitedSlugCharactersDisplay}")
            .Must(slug => !Uri.IsWellFormedUriString(slug, UriKind.Absolute))
            .WithMessage("'slug' cannot be a fully qualified URI")
            .When(f => !string.IsNullOrEmpty(f.Slug));

        RuleFor(f => f.PublicId)
            .NotEmpty()
            .When(f => isFlatRequest && f.Parent == null && f.Slug == null)
            .WithMessage("'public ID' is required if the 'slug' and 'parent' are not specified");
    }
}

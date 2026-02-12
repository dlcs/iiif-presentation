using FluentValidation;
using Models.Database;

namespace API.Features.Manifest.Validators;

/// <summary>
/// Used for any validation that must be completed after implicit canvases have been given an order
/// </summary>
public class CanvasPaintingsValidator : AbstractValidator<IEnumerable<CanvasPainting>>
{
    public CanvasPaintingsValidator()
    {
        // This validator is technically checking for an invalid choice construct.  However,
        // This is a second validation after working out the mapping between the API request and the final
        // database class (with the first validation in the PresentationManifestValidator checking for this explicitly),
        // at which point the only possible reason for this to occur is that there's conflicting implicit
        // and explicit `canvasOrder` values
        RuleFor(m => m)
            .Must(lcp => !lcp.GroupBy(pr => pr.CanvasOrder)
                .Where(g => g.Count() > 1)
                .Any(grp => grp.Count() > 1 && grp.Any(pr => pr.ChoiceOrder == null)))
            .WithMessage("Detected conflicting implicit and explicit 'canvasOrder' values");
    }
}

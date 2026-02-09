using FluentValidation;
using Models.Database;

namespace API.Features.Manifest.Validators;

/// <summary>
/// Used for any validation that must be completed after implicit canvases have been given an order
/// </summary>
public class PresentationManifestDatabaseValidator : AbstractValidator<IEnumerable<CanvasPainting>>
{
    public PresentationManifestDatabaseValidator()
    {
        RuleFor(m => m)
            .Must(lcp => !lcp.GroupBy(pr => pr.CanvasOrder)
                .Where(g => g.Count() > 1)
                .Any(grp => grp.Count() > 1 &&  grp.Any(pr => pr.ChoiceOrder == null)))
            .WithMessage("Painted resources cannot have a null 'choiceOrder' within a detected choice construct. This can happen when implicit and explicit 'canvasOrder' values conflict");
    }
}

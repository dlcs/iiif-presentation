using FluentValidation;
using Models.API.Manifest;

namespace API.Features.Manifest.Validators;

/// <summary>
/// Used by any validation that must be completed after implicit canvases have been given an order
/// or when all canvases have been given an explicit canvasOrder
/// </summary>
public class CanvasOrderSpecifiedValidator : AbstractValidator<List<CanvasPainting?>>
{
    public CanvasOrderSpecifiedValidator()
    {
        RuleFor(m => m)
            .Must(lpr => !lpr.Where(pr => pr?.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasOrder)
                .Where(g => g.Count() > 1)
                .Any(grp => grp.Select(pr => pr.CanvasId).Distinct().Count() > 1))
            .WithMessage("Canvases that share 'canvasOrder' must have same 'canvasId'");
    }
}

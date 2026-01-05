using API.Features.Storage.Validators;
using API.Infrastructure.Validation;
using API.Settings;
using Core.Helpers;
using FluentValidation;
using Microsoft.Extensions.Options;
using Models.API.Manifest;

namespace API.Features.Manifest.Validators;

public class PresentationManifestValidator : AbstractValidator<PresentationManifest>
{
    public PresentationManifestValidator(IOptions<ApiSettings> options)
    {
        When(m => !m.PaintedResources.IsNullOrEmpty(), PaintedResourcesValidation);
        RuleFor(c => c).SetValidator(new PresentationValidator());
    }

    // Validation rules specific to PaintedResources only
    private void PaintedResourcesValidation()
    {
        RuleForEach(a => a.PaintedResources)
            .Must(pr => pr.CanvasPainting?.ChoiceOrder != 0)
            .WithMessage("Canvases cannot have a 'choiceOrder' of 0");
        
        RuleFor(m => m.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
                .Where(g => g.Count() > 1)
                .Any(grp => grp.Select(pr => pr.CanvasPainting.CanvasId).Distinct().Count() > 1))
            .When(m => !m.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("Canvases that share 'canvasOrder' must have same 'canvasId'");

        RuleFor(m => m.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
                .Any(grp =>
                {
                    var distinctChoiceOrder = grp.Select(pr => pr.CanvasPainting.ChoiceOrder).Distinct().Count();
                    return distinctChoiceOrder != grp.Count();
                }))
            .When(m => m.PaintedResources.All(pr => pr.CanvasPainting?.ChoiceOrder != null))
            .WithMessage("'choiceOrder' cannot be a duplicate within a 'canvasOrder'");
        
        RuleForEach(f => f.PaintedResources)
            .Where(pr => pr.CanvasPainting != null)
            .Must(pr => pr.CanvasPainting!.StaticHeight.HasValue == pr.CanvasPainting.StaticWidth.HasValue)
            .WithMessage(
                "'static_width' and 'static_height' have to be both set or both absent within a 'canvasPainting'");
    }
}

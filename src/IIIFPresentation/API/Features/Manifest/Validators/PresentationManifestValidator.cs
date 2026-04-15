using API.Features.Storage.Validators;
using API.Settings;
using Core.Helpers;
using FluentValidation;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Features.Manifest.Validators;

public class PresentationManifestValidator : AbstractValidator<PresentationManifest>
{
    public PresentationManifestValidator(IOptions<ApiSettings> options)
    {
        When(m => !m.PaintedResources.IsNullOrEmpty(), PaintedResourcesValidation);
        RuleFor(c => c).SetValidator(new PresentationValidator());
        
        RuleFor(m => m.Items)
            .Must(i => i.DistinctBy(c => c.Id).Count() == i.Count)
            .When(m => !m.Items.IsNullOrEmpty())
            .WithMessage("The id in 'items' contains duplicates, which is not allowed");
    }

    // Validation rules specific to PaintedResources only
    private void PaintedResourcesValidation()
    {
        When(m => m.PaintedResources?.Any(pr => pr.Asset?[AssetProperties.Adjuncts] != null) == true,
            AdjunctsValidation);
        
        RuleForEach(m => m.PaintedResources)
            .Must(pr => pr.CanvasPainting?.CanvasOrder != null)
            .When(m => m.PaintedResources != null && m.PaintedResources.Any(pr => pr.CanvasPainting is { CanvasOrder: not null }))
            .WithMessage("'canvasOrder' is required on all resources when used in at least one");
        
        RuleForEach(a => a.PaintedResources)
            .Where(pr => pr.CanvasPainting?.ChoiceOrder != null)
            .Must(pr => pr.CanvasPainting?.ChoiceOrder > 0)
            .WithMessage("Canvases cannot have a 'choiceOrder' of 0 or less");
        
        RuleFor(m => m.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
                .Where(g => g.Count() > 1)
                .Any(grp => grp.Select(pr => pr.CanvasPainting.CanvasId).Distinct().Count() > 1))
            .When(m => !m.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("Canvases that share 'canvasOrder' must have same 'canvasId'");
            
        RuleFor(m => m.PaintedResources)
            .Must(lpr => !lpr
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
                .Where(g => g.Count() == 1)
                .Any(grp => grp.Any(pr => pr.CanvasPainting?.ChoiceOrder > 0)))
            .When(m => !m.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("'choiceOrder' must be null when there is a single painted resource with that 'canvasOrder'");

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
        
        RuleFor(m => m.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting!.CanvasOrder != null && pr.CanvasPainting.CanvasId != null && pr.CanvasPainting.ChoiceOrder == null)
                .GroupBy(pr => new {pr.CanvasPainting!.CanvasId, pr.CanvasPainting.CanvasOrder})
                .Any(grp => grp.Count() > 1))
            .When(m => !m.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("Painted resources cannot have a null 'choiceOrder' within a detected choice construct");
        
        RuleForEach(f => f.PaintedResources)
            .Where(pr => pr.CanvasPainting != null)
            .Must(pr => pr.CanvasPainting!.StaticHeight.HasValue == pr.CanvasPainting.StaticWidth.HasValue)
            .WithMessage(
                "'static_width' and 'static_height' have to be both set or both absent within a 'canvasPainting'");
     }
    
    // Validation rules specific to Adjuncts only
    private void AdjunctsValidation()
    {
        RuleForEach(m => m.PaintedResources)
            .Where(pr => pr.Asset?[AssetProperties.Adjuncts] is JArray)
            .ChildRules(pr =>
            {
                pr.RuleFor(r => r.Asset![AssetProperties.Adjuncts]!.ToObject<List<Adjunct>>())
                    .ForEach(adjunct => adjunct.SetValidator(new AdjunctValidator()));
            });
    }
}

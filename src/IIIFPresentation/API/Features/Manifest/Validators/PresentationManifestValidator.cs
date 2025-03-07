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
        var settings = options.Value;
        
        RuleFor(f => f.Parent).NotEmpty().WithMessage("Requires a 'parent' to be set");
        RuleFor(f => f.Slug).NotEmpty().WithMessage("Requires a 'slug' to be set")
            .Must(slug => !SpecConstants.ProhibitedSlugs.Contains(slug!))
            .WithMessage("'slug' cannot be one of prohibited terms: '{PropertyValue}'");
        if (!settings.IgnorePaintedResourcesWithItems)
        {
            RuleFor(f => f.Items).Empty()
                .When(f => !f.PaintedResources.IsNullOrEmpty())
                .WithMessage("The properties \"items\" and \"paintedResource\" cannot be used at the same time");
        }
        
        RuleFor(f => f.PaintedResources)
            .Must(lpr => lpr.Any(pr => pr.CanvasPainting.CanvasOrder != null) 
                         != lpr.Any(pr => pr.CanvasPainting.CanvasOrder == null))
            .When(f => !f.PaintedResources.IsNullOrEmpty() && !f.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("'canvasOrder' is required on all resources when used in at least one");
        
        RuleFor(f => f.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder).Where(g => g.Count() > 1)
                .Any(s => s.Any(g => g.CanvasPainting.ChoiceOrder == null)))
            .When(f => !f.PaintedResources.IsNullOrEmpty() && !f.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("'choiceOrder' cannot be null within a duplicate 'canvasOrder'");
        
        RuleFor(f => f.PaintedResources)
            .Must(lpr => !lpr.Where(pr => pr.CanvasPainting.CanvasOrder != null)
                .GroupBy(pr => pr.CanvasPainting.CanvasOrder)
                .Any(s =>
                {
                    var distinctChoiceOrder = s.Select(pr => pr.CanvasPainting.ChoiceOrder).Distinct().Count();

                    return distinctChoiceOrder != s.Count();
                }))
            .When(f => !f.PaintedResources.IsNullOrEmpty() && !f.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("'choiceOrder' cannot be a duplicate within a 'canvasOrder'");

        RuleFor(f => f.PaintedResources)
            .Must(lpr =>
                lpr!.All(
                    // either both have value, or none
                    pr => pr.CanvasPainting!.StaticHeight.HasValue == pr.CanvasPainting.StaticWidth.HasValue))
            .When(f => !f.PaintedResources.IsNullOrEmpty() && f.PaintedResources.All(pr => pr.CanvasPainting != null))
            .WithMessage(
                "'static_width' and 'static_height' have to be both set or both absent within a 'canvasPainting'");
        
        RuleFor(f => f.PaintedResources)
            .Must(lpr => lpr.Any(pr => pr.CanvasPainting.CanvasId != null) 
                         != lpr.Any(pr => pr.CanvasPainting.CanvasId == null))
            .When(f => !f.PaintedResources.IsNullOrEmpty() && !f.PaintedResources.Any(pr => pr.CanvasPainting == null))
            .WithMessage("'canvasId' is required on all resources when used in at least one");

    }
}

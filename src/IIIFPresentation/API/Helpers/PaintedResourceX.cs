using Core.Helpers;
using Models.API.Manifest;

namespace API.Helpers;

public static class PaintedResourceX
{
    public static bool HasAsset(this List<PaintedResource>? paintedResources)
    {
        return paintedResources != null && paintedResources.Any(p => p.Asset != null);
    }
    
    /// <summary>
    /// For the provided paintedResources, calculate how many canvases will be generated when processing. A canvas is
    /// required for each unique CanvasOrder value, or 1 for each null CanvasOrder
    /// </summary>
    public static int GetRequiredNumberOfCanvases(this List<PaintedResource>? paintedResources)
    {
        // no need to generate a canvas id for canvases that have an id, as we'll use that
        if (paintedResources.IsNullOrEmpty() || paintedResources.Any(pr => pr.CanvasPainting?.CanvasId != null)) return 0;

        // Random number that counts down for each null CanvasOrder to treat it as unique
        int counter = -10000;
        return paintedResources.DistinctBy(pr => pr.CanvasPainting?.CanvasOrder ?? --counter).Count();
    }
}

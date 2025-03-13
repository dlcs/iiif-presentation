using Core.Helpers;
using Models.Database;

namespace API.Helpers;

public static class CanvasPaintingX
{
    /// <summary>
    /// For the provided canvasPainting, calculate how many canvases will be generated when processing. A canvas is
    /// required for each unique CanvasOrder value, or 1 for each null CanvasOrder
    /// </summary>
    public static int GetRequiredNumberOfCanvases(this List<CanvasPainting>? paintedResources)
    {
        if (paintedResources.IsNullOrEmpty()) return 0;
        return paintedResources.Where(pr => pr.Id == null).DistinctBy(pr => pr.CanvasOrder).Count();
    }
}

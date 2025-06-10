using Core.Helpers;
using Models.Database;

namespace API.Helpers;

public static class CanvasPaintingX
{
    /// <summary>
    /// For the provided canvasPainting, calculate how many new canvas ids will be required when processing.
    /// A canvasId is required for
    ///   each unique CanvasOrder value, or 1 for each null CanvasOrder, OR
    ///   each unique CanvasOriginalId
    /// </summary>
    /// <remarks>Items sharing a CanvasOriginalId must have come in on the same canvas</remarks>
    public static int GetRequiredNumberOfCanvasIds(this List<CanvasPainting>? canvasPainting) =>
        canvasPainting.IsNullOrEmpty()
            ? 0
            : canvasPainting
                .Where(cp => string.IsNullOrEmpty(cp.Id))
                .DistinctBy(GetGroupingForIdAssignment)
                .Count();

    /// <summary>
    /// Get value that <see cref="CanvasPainting"/> can be grouped by when generating id, when we haven't been provided
    /// with an explicit canvasId. If we have a CanvasOriginalId then use that, else fall back to CanvasOrder.
    /// </summary>
    public static string GetGroupingForIdAssignment(this CanvasPainting canvasPainting) =>
        canvasPainting.CanvasOriginalId?.ToString() ?? canvasPainting.CanvasOrder.ToString();
}

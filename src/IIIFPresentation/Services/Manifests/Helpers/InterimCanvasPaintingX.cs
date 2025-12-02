using Core.Helpers;
using Models.Database;
using Models.DLCS;
using Services.Manifests.Model;

namespace Services.Manifests.Helpers;

public static class InterimCanvasPaintingX
{
    /// <summary>
    /// For the provided canvasPainting, calculate how many new canvas ids will be required when processing.
    /// A canvasId is required for
    ///   each unique CanvasOrder value, or 1 for each null CanvasOrder, OR
    ///   each unique CanvasOriginalId
    /// </summary>
    /// <remarks>Items sharing a CanvasOriginalId must have come in on the same canvas</remarks>
    public static int GetRequiredNumberOfCanvasIds(this List<InterimCanvasPainting>? canvasPainting) =>
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
    public static string GetGroupingForIdAssignment(this InterimCanvasPainting canvasPainting) =>
        canvasPainting.CanvasOriginalId?.ToString() ?? canvasPainting.CanvasOrder.ToString();
    
    /// <summary>
    /// Checks a list of canvas paintings for if they contain a specific id
    /// </summary>
    public static bool ContainsId(this List<InterimCanvasPainting>? canvasPainting, string? id) =>
        canvasPainting?.Any(cp => cp.Id?.Equals(id, StringComparison.OrdinalIgnoreCase) ?? false) ?? false;

    public static CanvasPainting ToCanvasPainting(this InterimCanvasPainting interimCanvasPainting, int? space)
    {
        interimCanvasPainting.SuspectedSpace ??= space;

        var assetId = interimCanvasPainting is { SuspectedAssetId: not null, SuspectedSpace: not null }
            ? new AssetId(interimCanvasPainting.CustomerId, interimCanvasPainting.SuspectedSpace.Value,
                interimCanvasPainting.SuspectedAssetId)
            : null;

        return GenerateCanvasPainting(interimCanvasPainting, assetId);
    }

    private static CanvasPainting GenerateCanvasPainting(InterimCanvasPainting interimCanvasPainting, AssetId? assetId)
    {
        return new CanvasPainting
        {
            Id = interimCanvasPainting.Id,
            CustomerId = interimCanvasPainting.CustomerId,
            Label = interimCanvasPainting.Label,
            CanvasLabel = interimCanvasPainting.CanvasLabel,
            CanvasOrder = interimCanvasPainting.CanvasOrder,
            ChoiceOrder = interimCanvasPainting.ChoiceOrder,
            Thumbnail = interimCanvasPainting.Thumbnail,
            StaticHeight = interimCanvasPainting.StaticHeight,
            StaticWidth = interimCanvasPainting.StaticWidth,
            Target = interimCanvasPainting.Target,
            AssetId = assetId,
            Ingesting = interimCanvasPainting.Ingesting,
            CanvasOriginalId = interimCanvasPainting.CanvasOriginalId,
        };
    }

    public static List<CanvasPainting> ConvertInterimCanvasPaintings(
        this List<InterimCanvasPainting> interimCanvasPaintings, int? space)
    {
        return interimCanvasPaintings.Select(icp =>
        {
            if (icp.SuspectedAssetId != null && icp.SuspectedSpace == null)
            {
                space.ThrowIfNull($"Space of canvas {icp.Id} cannot be inferred and cannot be null");
                icp.SuspectedSpace = space!.Value;
            }

            var assetId = icp.SuspectedAssetId != null ? new AssetId(icp.CustomerId, icp.SuspectedSpace!.Value, icp.SuspectedAssetId) : null;
            
            return GenerateCanvasPainting(icp, assetId);
        }).ToList();
    }

    public static List<InterimCanvasPainting>? GetItemsWithSuspectedAssets(
        this List<InterimCanvasPainting>? interimCanvasPaintings) =>
        interimCanvasPaintings?.Where(icp =>
            icp is { SuspectedAssetId: not null, CanvasPaintingType: CanvasPaintingType.Items }).ToList();

    public static IEnumerable<InterimCanvasPainting> OrderInterimCanvasPaintings(
        this IEnumerable<InterimCanvasPainting> interimCanvasPaintings) =>
        interimCanvasPaintings.OrderBy(cp => cp.CanvasOrder).ThenBy(cp => cp.ChoiceOrder);
}

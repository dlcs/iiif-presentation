using Core.Helpers;
using IIIF.Presentation.V3.Strings;
using Models.Database;
using Models.DLCS;

namespace Services.Manifests.Model;

public class InterimCanvasPainting : CanvasPainting
{
    /// <summary>
    /// A potential space for the asset
    /// </summary>
    public int? SuspectedSpace { get; set; }
    
    /// <summary>
    /// A potential id for the asset
    /// </summary>
    public string? SuspectedAssetId { get; set; }
    
    /// <summary>
    /// Where is this canvas painting record from?
    /// </summary>
    public CanvasPaintingType CanvasPaintingType { get; set; }
    
    /// <summary>
    /// Whether the ordering for this record is set explicitly, or set by the API
    /// </summary>
    public bool ImplicitOrder { get; set; }
}

public enum CanvasPaintingType
{
    Unknown,
    /// <summary>
    /// This canvas painting is driven from items
    /// </summary>
    Items,
    /// <summary>
    /// This canvas painting is driven from painted resources
    /// </summary>
    PaintedResource,
    /// <summary>
    /// This canvas painting is a join between painted resources and items
    /// </summary>
    Mixed
}

public static class InterimCanvasPaintingX
{
    /// <summary>
    /// Checks a list of canvas paintings for if they contain a specific id
    /// </summary>
    public static bool ContainsId(this List<InterimCanvasPainting>? canvasPainting, string? id) =>
        canvasPainting?.Any(cp => cp.Id == id) ?? false;

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
}

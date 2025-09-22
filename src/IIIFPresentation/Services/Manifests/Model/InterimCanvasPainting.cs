using IIIF.Presentation.V3.Strings;
using Microsoft.AspNetCore.Http.HttpResults;
using Models.Database;
using Models.DLCS;

namespace Services.Manifests.Model;

public class InterimCanvasPainting
{
    /// <summary>
    /// Unique identifier for canvas on a manifest.
    /// </summary>
    /// <remarks>
    /// There can be multiple rows with the same CanvasId and ManifestId (e.g. if there are multiple choices)
    /// </remarks>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// The customer identifier
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// A fully qualified external URI specific with "items" property;
    /// </summary>
    /// <remarks>
    /// This can be an externally managed Id (ie on a separate domain) or an id for another Canvas managed by
    /// iiif-presentation
    /// </remarks>
    public Uri? CanvasOriginalId { get; set; }

    /// <summary>
    /// 0-based Canvas sequence order within a Manifest. This keeps incrementing for successive paintings on the same
    /// canvas, it is always >= number of canvases in the manifest. For most manifests, the number of rows equals the
    /// highest value of this. It stays the same for successive content resources within a Choice (see choice_order). It
    /// gets recalculated on a Manifest save by walking through the manifest.items, incrementing as we go.
    /// </summary>
    public int CanvasOrder { get; set; }

    /// <summary>
    /// 1-based. Normally null; a positive integer indicates that the asset is part of a Choice body. 
    /// Multiple choice bodies share same value of canvas_order. When the successive content resources are items in a
    /// Choice body, canvas_order holds constant and this row increments.
    /// </summary>
    public int? ChoiceOrder { get; set; }

    /// <summary>
    /// Optional URI of a thumbnail for canvas. 
    /// </summary>
    /// <remarks>Could this be derived from asset id in the future?</remarks>
    public Uri? Thumbnail { get; set; }

    /// <summary>
    /// Stored language map, is the same as the on the canvas, may be null where it is not contributing to the canvas,
    /// should be used for choice, multiples etc.
    /// </summary>
    public LanguageMap? Label { get; set; }

    /// <summary>
    /// Only needed if the canvas label is not to be the first asset label; multiple assets on a canvas use the first.
    /// </summary>
    public LanguageMap? CanvasLabel { get; set; }
    
    /// <summary>
    /// Null if Canvas fills whole canvas, otherwise a parseable IIIF selector (fragment or JSON)
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Unique identifier for this CanvasPainting object
    /// </summary>
    public int CanvasPaintingId { get; set; }
    
    public int? Space { get; set; }
    
    /// <summary>
    /// An asset id showing this asset is an internal item
    /// </summary>
    public string? AssetId { get; set; }
    
    /// <summary>
    /// Whether the asset is currently being ingested into the DLCS
    /// </summary>
    public bool Ingesting { get; set; }
    
    /// <summary>
    /// For spatial resources, the width of the resources on the canvas 
    /// </summary>
    public int? StaticWidth { get; set; }

    /// <summary>
    /// For spatial resources, the height of the resources on the canvas
    /// </summary>
    public int? StaticHeight { get; set; }

    /// <summary>
    ///     For temporal resources, the duration of the resources on the canvas
    /// </summary>
    public double? Duration { get; set; }
    
    /// <summary>
    /// Created date/time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    public DateTime Modified { get; set; }
    
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
    public static bool InterimCanvasPaintingContainsId(this List<InterimCanvasPainting>? canvasPainting, string? id) =>
        canvasPainting?.Any(cp => cp.Id == id) ?? false;
    
    /// <summary>
    /// Update current object, with values from specified <see cref="CanvasPainting"/>
    /// Modified date is _always_ updated - whether there were changes or not
    /// </summary>
    public static InterimCanvasPainting UpdateFrom(this InterimCanvasPainting canvasPainting, InterimCanvasPainting updated)
    {
        canvasPainting.Label = updated.Label;
        canvasPainting.CanvasLabel = updated.CanvasLabel;
        canvasPainting.CanvasOrder = updated.CanvasOrder;
        canvasPainting.ChoiceOrder = updated.ChoiceOrder;
        canvasPainting.Thumbnail = updated.Thumbnail;
        canvasPainting.StaticHeight = updated.StaticHeight;
        canvasPainting.StaticWidth = updated.StaticWidth;
        canvasPainting.Target = updated.Target;
        canvasPainting.Modified = DateTime.UtcNow;
        canvasPainting.AssetId = updated.AssetId;
        canvasPainting.Ingesting = updated.Ingesting;
        canvasPainting.CanvasOriginalId = updated.CanvasOriginalId;
        if (!string.IsNullOrEmpty(updated.Id)) canvasPainting.Id = updated.Id;
        return canvasPainting;
    } 

    public static CanvasPainting ConvertInterimCanvasPainting(this InterimCanvasPainting interimCanvasPainting, int? space)
    {
        interimCanvasPainting.Space ??= space;
        
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
            Created = interimCanvasPainting.Created,
            Modified = interimCanvasPainting.Modified,
            Target = interimCanvasPainting.Target,
            AssetId = interimCanvasPainting is { AssetId: not null, Space: not null } ? 
                new AssetId(interimCanvasPainting.CustomerId, interimCanvasPainting.Space.Value, interimCanvasPainting.AssetId) : null,
            Ingesting = interimCanvasPainting.Ingesting,
            CanvasOriginalId = interimCanvasPainting.CanvasOriginalId,
        };
    }
    
    public static List<CanvasPainting> ConvertInterimCanvasPaintings(
        this List<InterimCanvasPainting> interimCanvasPaintings, int? space)
    {
        return interimCanvasPaintings.Select(i =>
        {
            if (i.AssetId != null && i.Space == null)
            {
                if (space == null)
                {
                    throw new ArgumentNullException(nameof(i.Space), $"space of canvas {i.Id} cannot be inferred and cannot be null");
                }
                
                i.Space = space.Value;
            }
            
            return new CanvasPainting
            {
                Id = i.Id,
                CustomerId = i.CustomerId,
                Label = i.Label,
                CanvasLabel = i.CanvasLabel,
                CanvasOrder = i.CanvasOrder,
                ChoiceOrder = i.ChoiceOrder,
                Thumbnail = i.Thumbnail,
                StaticHeight = i.StaticHeight,
                StaticWidth = i.StaticWidth,
                Created = i.Created,
                Modified = i.Modified,
                Target = i.Target,
                AssetId = i.AssetId != null ? new AssetId(i.CustomerId, i.Space!.Value, i.AssetId) : null,
                Ingesting = i.Ingesting,
                CanvasOriginalId = i.CanvasOriginalId,
            };
        }).ToList();
    }
}

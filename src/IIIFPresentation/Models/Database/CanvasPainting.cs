using IIIF.Presentation.V3.Strings;
using Models.Database.Collections;

namespace Models.Database;

/// <summary>
/// Table that allows us to express multiple content resources on a Canvas, which may or may not target the full Canvas,
/// and which may or may not be Choice bodies.
/// </summary>
/// <remarks>
/// CanvasId, CustomerId and ManifestId do not use "required" as they are initially created as partial entities and
/// hydrated later
/// </remarks>
public class CanvasPainting : IIdentifiable
{
    /// <summary>
    /// Unique identifier for canvas on a manifest.
    /// </summary>
    /// <remarks>
    /// There can be multiple rows with the same CanvasId and ManifestId (e.g. if there are multiple choices_
    /// </remarks>
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// Id of related manifest
    /// </summary>
    public string ManifestId { get; set; } = null!;
    
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
    /// For spatial resources, the width of the resources on the canvas 
    /// </summary>
    public int? StaticWidth { get; set; }

    /// <summary>
    /// For spatial resources, the height of the resources on the canvas
    /// </summary>
    public int? StaticHeight { get; set; }
    
    public Manifest? Manifest { get; set; }
    
    /// <summary>
    /// Created date/time
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified date/time
    /// </summary>
    public DateTime Modified { get; set; }
}

public class CanvasPaintingEqualityComparer : IEqualityComparer<CanvasPainting>
{
    /// <summary>
    /// Default instance of <see cref="CanvasPaintingEqualityComparer"/>
    /// </summary>
    public static readonly CanvasPaintingEqualityComparer Instance = new();
    
    public bool Equals(CanvasPainting? x, CanvasPainting? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null) return false;
        if (y is null) return false;
        if (x.GetType() != y.GetType()) return false;
        return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.ManifestId, y.ManifestId, StringComparison.OrdinalIgnoreCase) &&
               x.CustomerId == y.CustomerId && Equals(x.CanvasOriginalId, y.CanvasOriginalId) &&
               x.CanvasOrder == y.CanvasOrder && x.ChoiceOrder == y.ChoiceOrder;
    }

    public int GetHashCode(CanvasPainting obj)
    {
        var hashCode = new HashCode();
        hashCode.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(obj.ManifestId, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(obj.CustomerId);
        hashCode.Add(obj.CanvasOriginalId);
        hashCode.Add(obj.CanvasOrder);
        hashCode.Add(obj.ChoiceOrder);
        return hashCode.ToHashCode();
    }
}
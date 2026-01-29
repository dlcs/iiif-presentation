using Models.Database;

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

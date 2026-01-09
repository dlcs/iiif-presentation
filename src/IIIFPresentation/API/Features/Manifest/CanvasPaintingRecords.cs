using API.Infrastructure.Requests;
using Models.API.General;
using Models.API.Manifest;
using Services.Manifests.Model;

namespace API.Features.Manifest;

/// <summary>
/// Records class containing details of canvas paintings that require further processing
/// </summary>
public class CanvasPaintingRecords
{
    public static CanvasPaintingRecords Failure(ModifyEntityResult<PresentationManifest, ModifyCollectionType> updateResult) =>
        new()
        {
            Error = updateResult
        };
    
    public static CanvasPaintingRecords Success(List<InterimCanvasPainting>? canvasPaintingsToAdd, List<InterimCanvasPainting>? itemsWithAssets) =>
        new()
        {
            CanvasPaintingsToAdd = canvasPaintingsToAdd,
            CanvasPaintingsThatContainItemsWithAssets = itemsWithAssets
        };
    
    /// <summary>
    /// An error that occurred during processing
    /// </summary>
    public ModifyEntityResult<PresentationManifest, ModifyCollectionType>? Error { get; private init; }

    /// <summary>
    /// Details of all canvas paintings that are considered to be "new"
    /// </summary>
    /// <remarks>Contains canvas paintings from both the items property AND canvas paintings directly</remarks>
    public List<InterimCanvasPainting>? CanvasPaintingsToAdd { get; private init; }
    
    /// <summary>
    /// Details of all canvas paintings that have assets identified in items, as opposed to the canvas paintings directly.
    /// </summary>
    /// <remarks>This can contain modified records if the item has been identified as an update</remarks>
    public List<InterimCanvasPainting>? CanvasPaintingsThatContainItemsWithAssets { get; private init; }
}

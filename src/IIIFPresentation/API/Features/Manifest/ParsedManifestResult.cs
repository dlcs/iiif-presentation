using API.Infrastructure.Requests;
using Models.API.General;
using Services.Manifests.Model;

namespace API.Features.Manifest;

/// <summary>
/// Records class containing details of various things that require further processing, such as canvas paintings
/// and adjunct interactions
/// </summary>
public class ParsedManifestResult
{
    public static ParsedManifestResult Failure(ModifyEntityResult<ModifyCollectionType> updateResult) =>
        new()
        {
            Error = updateResult
        };
    
    public static ParsedManifestResult Success(List<InterimCanvasPainting>? canvasPaintingsToAdd, List<InterimCanvasPainting>? itemsWithAssets, List<AdjunctInteraction>? adjunctInteractions) =>
        new()
        {
            CanvasPaintingsToAdd = canvasPaintingsToAdd,
            CanvasPaintingsThatContainItemsWithAssets = itemsWithAssets,
            AdjunctInteractions = adjunctInteractions
        };
    
    /// <summary>
    /// An error that occurred during processing
    /// </summary>
    public ModifyEntityResult<ModifyCollectionType>? Error { get; private init; }

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
    
    /// <summary>
    /// Details of all adjunct interactions that are needed to work out if adjuncts are new or a replacement.
    /// </summary>
    public List<AdjunctInteraction>? AdjunctInteractions { get; private init; }
}

using API.Infrastructure.Requests;
using Models.API.General;
using Models.API.Manifest;
using Models.DLCS;
using Services.Manifests.Model;

namespace API.Features.Manifest;

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
            ItemsWithAssets = itemsWithAssets
        };
    
    public ModifyEntityResult<PresentationManifest, ModifyCollectionType>? Error { get; set; }

    public List<InterimCanvasPainting>? CanvasPaintingsToAdd { get; set; }
    
    public List<InterimCanvasPainting>? ItemsWithAssets { get; set; }
}

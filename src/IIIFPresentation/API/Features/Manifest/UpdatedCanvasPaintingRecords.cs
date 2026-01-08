using API.Infrastructure.Requests;
using Models.API.General;
using Models.API.Manifest;
using Models.DLCS;
using Services.Manifests.Model;

namespace API.Features.Manifest;

public class UpdatedCanvasPaintingRecords
{
    public static UpdatedCanvasPaintingRecords Failure(ModifyEntityResult<PresentationManifest, ModifyCollectionType> updateResult) =>
        new()
        {
            Error = updateResult
        };
    
    public static UpdatedCanvasPaintingRecords Success(List<InterimCanvasPainting>? canvasPaintingsToAdd, List<AssetId>? assetsIdentifiedInItems) =>
        new()
        {
            CanvasPaintingsToAdd = canvasPaintingsToAdd,
            AssetsIdentifiedInItems = assetsIdentifiedInItems
        };
    
    public ModifyEntityResult<PresentationManifest, ModifyCollectionType>? Error { get; set; }

    public List<InterimCanvasPainting>? CanvasPaintingsToAdd { get; set; }
    
    public List<AssetId>? AssetsIdentifiedInItems { get; set; }
}

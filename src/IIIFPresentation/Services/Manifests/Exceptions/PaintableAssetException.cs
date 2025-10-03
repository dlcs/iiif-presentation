using Core.Exceptions;
using Models.DLCS;

namespace Services.Manifests.Exceptions;

public class PaintableAssetException(AssetId fromBody, AssetId fromServices) : PresentationException(
    $"Suspected asset from image body ({fromBody}) and services ({fromServices}) point to different managed assets")
{
    public AssetId FromBody { get; } = fromBody;
    public AssetId FromServices { get; } = fromServices;
}

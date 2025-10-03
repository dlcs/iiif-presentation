using Core.Exceptions;
using Models.DLCS;

namespace Services.Manifests.Exceptions;

public class PaintableAssetException : PresentationException
{
    public AssetId FromBody { get; set; }
    public AssetId FromServices { get; set; }
    
    public PaintableAssetException(AssetId fromBody, AssetId fromServices)
    {
        FromBody = fromBody;
        FromServices = fromServices;
    }
}

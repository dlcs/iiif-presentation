using Core.Exceptions;
using Models.DLCS;

namespace Services.Manifests.Exceptions;

public class PaintableAssetException(AssetId firstIdentifiedAsset, AssetId secondIdentifiedAsset, string message) : PresentationException(message)
{
    public AssetId FirstIdentifiedAsset { get; } = firstIdentifiedAsset;
    public AssetId SecondIdentifiedAsset { get; } = secondIdentifiedAsset;
}

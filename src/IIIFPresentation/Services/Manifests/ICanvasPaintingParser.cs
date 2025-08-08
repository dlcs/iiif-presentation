using IIIF.Presentation.V3.Annotation;
using Models.API.Manifest;
using Models.DLCS;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Services.Manifests;

/// <summary>
/// For classes that parse <see cref="PresentationManifest"/> to extract <see cref="CanvasPainting"/> objects.
/// </summary>
public interface ICanvasPaintingParser
{
    IEnumerable<CanvasPainting> ParseToCanvasPainting(PresentationManifest manifest, int customer,
        Dictionary<IPaintable, AssetId> recognizedItemsAssets);
}

using Models.API.Manifest;
using CanvasPainting = Models.Database.CanvasPainting;

namespace Repository.Manifests;

/// <summary>
/// For classes that parse <see cref="PresentationManifest"/> to extract <see cref="CanvasPainting"/> objects.
/// </summary>
public interface ICanvasPaintingParser
{
    IEnumerable<CanvasPainting> ParseToCanvasPainting(PresentationManifest manifest, int customer);
}

using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Helpers;

/// <summary>
/// Updates assets and canvas paintings with details of the manifest space
/// </summary>
public static class SpaceHelper
{
    public static void UpdateAssets(JObject[] assetsWithoutSpaces, int spaceId)
    {
        foreach (var asset in assetsWithoutSpaces)
            asset.Add(AssetProperties.Space, spaceId);
    }
    
    public static void UpdateCanvasPaintings(List<CanvasPainting> canvasPaintings, int? spaceId)
    {
        if (!spaceId.HasValue) return;

        foreach (var canvasPainting in canvasPaintings.Where(canvasPainting => canvasPainting.AssetId?.Space == -1))
        {
            canvasPainting.AssetId = new AssetId(canvasPainting.AssetId!.Customer, spaceId.Value, canvasPainting.AssetId.Asset);
        }
    }
}

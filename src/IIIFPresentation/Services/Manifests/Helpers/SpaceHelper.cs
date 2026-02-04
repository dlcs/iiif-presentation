using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace Services.Manifests.Helpers;

/// <summary>
/// Updates assets and canvas paintings with details of the manifest space
/// </summary>
public static class SpaceHelper
{
    /// <summary>
    /// The space used to signal that the space needs to be populated following DLCS interactions
    /// </summary>
    /// <remarks>This is a negative number as negative numbers are disallowed for a space id</remarks>
    public static int DefaultSpaceForLaterPopulation => -1;

    /// <summary>
    /// Updates asset JObjects that don't have a space with a spoecified space
    /// </summary>
    /// <param name="assetsWithoutSpaces">The assets that don't have a space id attached</param>
    /// <param name="spaceId">The space id to set</param> 
    public static void UpdateAssets(JObject[] assetsWithoutSpaces, int spaceId)
    {
        foreach (var asset in assetsWithoutSpaces) asset.Add(AssetProperties.Space, spaceId);
    }
    
    /// <summary>
    /// Updates canvas paintings with a specified space id where they have been marked for population following DLCS
    /// interactions
    /// </summary>
    /// <param name="canvasPaintings">The canvas paintings to check</param>
    /// <param name="spaceId">The space id to set</param>
    public static void UpdateCanvasPaintings(List<CanvasPainting> canvasPaintings, int? spaceId)
    {
        if (!spaceId.HasValue) return;
        foreach (var canvasPainting in canvasPaintings.Where(canvasPainting => canvasPainting.AssetId?.Space == DefaultSpaceForLaterPopulation))
        {
            canvasPainting.AssetId = new AssetId(canvasPainting.AssetId!.Customer, spaceId.Value, canvasPainting.AssetId.Asset);
        }
    }
}

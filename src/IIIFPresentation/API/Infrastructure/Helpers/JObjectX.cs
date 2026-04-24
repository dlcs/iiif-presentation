using Core.Helpers;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Services.Manifests.Model;

namespace API.Infrastructure.Helpers;

public static class JObjectX
{
    /// <summary>
    /// Get specified property value from jObject. Null if not found
    /// </summary>
    public static AssetId GetAssetId(this JObject jObject, int customerId)
        => AssetId.FromString(
            $"{customerId}/{jObject.GetRequiredValue(AssetProperties.Space)}/{jObject.GetRequiredValue(AssetProperties.Id)}");

    public static void SetExistingAdjunctIds(this JObject jObject, IList<AdjunctInteraction>? adjunctInteractions, AssetId assetId)
    {
        // fill out existing adjunct id's with any asset we've retrieved as part of checking for known assets
        var adjunctInteraction = adjunctInteractions?.SingleOrDefault(a => a.AssetId == assetId);

        if (adjunctInteraction != null)
        {
            adjunctInteraction.ExistingAdjunctIds = jObject[AssetProperties.Adjuncts] is JArray existingAdjuncts
                ? existingAdjuncts
                    .Select(a => a[AdjunctProperties.Id]?.Value<string>())
                    .OfType<string>()
                    .ToList()
                : [];
        }
    }
}

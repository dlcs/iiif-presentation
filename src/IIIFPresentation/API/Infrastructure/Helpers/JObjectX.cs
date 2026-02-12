using Core.Helpers;
using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Infrastructure.Helpers;

public static class JObjectX
{
    /// <summary>
    /// Get specified property value from jObject. Null if not found
    /// </summary>
    public static AssetId GetAssetId(this JObject jObject, int customerId)
        => AssetId.FromString(
            $"{customerId}/{jObject.GetRequiredValue(AssetProperties.Space)}/{jObject.GetRequiredValue(AssetProperties.Id)}");
}

using IIIF.Presentation.V3;
using Newtonsoft.Json.Linq;

namespace Services.Manifests.Helpers;

public static class ManifestContextHelper
{
    /// <summary>
    /// Extracts all context strings from a manifest's Context property,
    /// normalising the various representations (string, array, JArray, JValue).
    /// </summary>
    public static IEnumerable<string> GetContextStrings(this Manifest manifest) =>
        manifest.Context switch
        {
            null => [],
            string str => [str],
            IEnumerable<string> enumerable => enumerable,
            JArray jArray => jArray.Values<string>().Where(s => s != null).Select(s => s!),
            JValue { Type: JTokenType.String } jValue when jValue.ToString() is { } plain => [plain],
            _ => []
        };
}

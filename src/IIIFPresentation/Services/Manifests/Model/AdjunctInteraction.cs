using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace Services.Manifests.Model;

public class AdjunctInteraction
{
    public required AssetId AssetId { get; set; }
    public required List<JObject> Adjuncts { get; init; }
    public List<string>? ExistingAdjunctIds { get; set; }
}

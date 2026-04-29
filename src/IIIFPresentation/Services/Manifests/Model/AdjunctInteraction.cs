using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace Services.Manifests.Model;

/// <summary>
/// Tracks the adjunct assets associated with a single asset, used to reconcile what needs to be
/// added/modified or deleted when a manifest is written.
/// </summary>
public class AdjunctInteraction
{
    /// <summary>
    /// The asset this adjunct belongs to.
    /// </summary>
    public required AssetId AssetId { get; set; }

    /// <summary>
    /// Adjunct payloads to be added/modified into IIIF-CS for this asset.
    /// </summary>
    public required List<JObject> Adjuncts { get; init; }

    /// <summary>
    /// Adjunct ids already registered to the asset.
    /// </summary>
    public List<string>? ExistingAdjunctIds { get; set; }
}

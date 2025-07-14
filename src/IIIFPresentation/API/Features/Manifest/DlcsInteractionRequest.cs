using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Features.Manifest;

public class DlcsInteractionRequest (JObject asset, IngestType ingest, 
    bool patch, AssetId assetId)
{
    /// <summary>
    /// The asset itself
    /// </summary>
    public JObject Asset { get; } = asset;

    /// <summary>
    /// The type of ingestion this asset requires
    /// </summary>
    public IngestType Ingest { get; } = ingest;
        
    /// <summary>
    /// Whether to patch the manifest id
    /// </summary>
    public bool Patch { get; } = patch;

    /// <summary>
    /// The asset id, to save pulling it out of the asset
    /// </summary>
    public AssetId AssetId { get; } = assetId;
}

public enum IngestType
{
    // do not ingest
    NoIngest,
    // ingest with a manifest id
    ManifestId,
    // ingest without a manifest id
    NoManifestId
}

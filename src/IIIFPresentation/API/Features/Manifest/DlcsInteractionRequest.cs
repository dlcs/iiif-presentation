using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Features.Manifest;

public class DlcsInteractionRequest(JObject asset, IngestType ingest, 
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
    /// <summary>
    /// Do not ingest asset.
    /// </summary>
    NoIngest,
    
    /// <summary>
    /// Ingest asset and set manifest id for Asset.
    /// This is used when the Asset is not already part of the Manifest - it is new or already associated
    /// with a different Manifest.
    /// </summary>
    ManifestId,
    
    /// <summary>
    /// Ingest asset without updating manifest id for Asset.
    /// This is used when the Asset is already part of the Manifest (e.g. for reingest scenarios)
    /// </summary>
    NoManifestId
}

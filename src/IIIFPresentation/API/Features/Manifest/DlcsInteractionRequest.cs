using Models.DLCS;
using Newtonsoft.Json.Linq;

namespace API.Features.Manifest;

public class DlcsInteractionRequest (JObject asset, IngestType ingest, 
    bool patch, AssetId assetId)
{
    public JObject Asset { get; set; } = asset;

    public IngestType Ingest { get; set; } = ingest;
        
    public bool Patch { get; set; } = patch;

    public AssetId AssetId { get; set; } = assetId;
}

public enum IngestType
{
    NoIngest,
    ManifestId,
    NoManifestId
}

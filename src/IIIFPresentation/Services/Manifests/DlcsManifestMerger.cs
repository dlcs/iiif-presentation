using DLCS.API;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Manifests;

/// <summary>
/// Augments a manifest with DLCS content - retrieving the assets tracked for the manifest from the DLCS orchestrator
/// and projecting them onto the manifest via <see cref="IManifestMerger"/>.
/// </summary>
public interface IDlcsManifestMerger : IManifestAugmentor
{
}

public class DlcsManifestMerger(
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IManifestMerger manifestMerger,
    ILogger<DlcsManifestMerger> logger)
    : IDlcsManifestMerger
{
    public async Task<Manifest> Augment(Manifest manifest, DbManifest dbManifest, CancellationToken cancellationToken)
    {
        var namedQueryManifest =
            await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);

        var mergeManifest = manifestMerger.MergeManifest(
            manifest,
            namedQueryManifest,
            dbManifest.CanvasPaintings,
            dbManifest.CustomerId,
            dbManifest.Id);

        logger.LogDebug("Merged Manifest with DLCS content {Manifest}", dbManifest.Id);
        return mergeManifest;
    }
}

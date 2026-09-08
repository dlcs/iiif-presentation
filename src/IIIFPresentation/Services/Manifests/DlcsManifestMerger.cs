using DLCS.API;
using DLCS.Exceptions;
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
        var namedQueryManifest = await RetrieveAssetsForManifest(dbManifest, cancellationToken);

        var mergeManifest = manifestMerger.MergeManifest(
            manifest,
            namedQueryManifest,
            dbManifest.CanvasPaintings,
            dbManifest.CustomerId,
            dbManifest.Id);

        logger.LogDebug("Merged Manifest with DLCS content {Manifest}", dbManifest.Id);
        return mergeManifest;
    }

    private async Task<Manifest?> RetrieveAssetsForManifest(DbManifest dbManifest, CancellationToken cancellationToken)
    {
        try
        {
            return await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);
        }
        catch (DlcsException dlcsException)
        {
            // The underlying DlcsException.Message isn't safe to pass straight back to the caller - it can be a raw
            // downstream response, and its status code isn't a reliable diagnosis (e.g. a 404 here also occurs for
            // a named query that legitimately matched no images). Log the detail for diagnosis and give a simple,
            // generic error in its place.
            logger.LogError(dlcsException,
                "Error retrieving assets from DLCS named query for manifest {ManifestId}, customer {CustomerId}",
                dbManifest.Id, dbManifest.CustomerId);

            throw new DlcsException($"A problem occurred communicating with the DLCS for manifest '{dbManifest.Id}'",
                dlcsException, dlcsException.StatusCode);
        }
    }
}

using DLCS.API;
using IIIF.Presentation.V3;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.Manifests;

/// <summary>
/// Merges a manifest with DLCS content - retrieving the assets tracked for the manifest from the DLCS orchestrator
/// and projecting them onto the manifest via <see cref="IManifestMerger"/>.
/// </summary>
public interface IDlcsManifestMerger
{
    /// <summary>
    /// Merge <paramref name="manifest"/> with the DLCS content tracked for <paramref name="dbManifest"/>.
    /// </summary>
    Task<Manifest> Merge(Manifest manifest, DbManifest dbManifest, CancellationToken cancellationToken);
}

public class DlcsManifestMerger(IDlcsOrchestratorClient dlcsOrchestratorClient, IManifestMerger manifestMerger)
    : IDlcsManifestMerger
{
    public async Task<Manifest> Merge(Manifest manifest, DbManifest dbManifest, CancellationToken cancellationToken)
    {
        var namedQueryManifest =
            await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);

        return manifestMerger.MergeManifest(
            manifest,
            namedQueryManifest,
            dbManifest.CanvasPaintings,
            dbManifest.CustomerId,
            dbManifest.Id);
    }
}

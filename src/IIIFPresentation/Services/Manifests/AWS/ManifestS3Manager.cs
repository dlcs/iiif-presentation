using AWS.Helpers;
using Core.Helpers;
using DLCS.API;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging;
using Repository.Paths;

namespace Services.Manifests.AWS;

/// <summary>
/// Responsible for managing manifests in S3
/// </summary>
public class ManifestS3Manager(
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IDlcsOrchestratorClient dlcsOrchestratorClient,
    IManifestMerger manifestMerger,
    ILogger<ManifestS3Manager> logger) : IManifestStorageManager
{
    /// <summary>
    /// Updates a manifest from the staging environment
    /// </summary>
    public async Task UpdateManifestInStorage(List<int> batches, Models.Database.Collections.Manifest dbManifest, CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating manifest {Manifest} in S3", dbManifest.Id);
        
        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, true, cancellationToken);
        manifest.ThrowIfNull(nameof(manifest), "Manifest was not found in staging location");
        
        var namedQueryManifest =
            await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);

        var mergedManifest = manifestMerger.ProcessCanvasPaintings(
            manifest!,
            namedQueryManifest,
            dbManifest.CanvasPaintings);

        await iiifS3.SaveIIIFToS3(mergedManifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            false, cancellationToken);

        await iiifS3.DeleteIIIFFromS3(dbManifest, true);
    }
}

public interface IManifestStorageManager
{
    public Task UpdateManifestInStorage(List<int> batches, Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken);
}

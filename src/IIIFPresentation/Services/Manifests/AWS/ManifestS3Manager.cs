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
    public async Task<Manifest> UpsertManifestInStorage(Manifest manifest,
        Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating manifest {Manifest} in S3", dbManifest.Id);

        var mergedManifest = await UpsertManifest(manifest, dbManifest, cancellationToken);

        return mergedManifest;
    }
    
    public async Task UpsertManifestFromStagingInStorage(Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating manifest {Manifest} in S3", dbManifest.Id);

        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, true, cancellationToken);
        manifest.ThrowIfNull(nameof(manifest), "Manifest was not found in staging location");

        await UpsertManifest(manifest!, dbManifest, cancellationToken);

        await iiifS3.DeleteIIIFFromS3(dbManifest, true);
    }
    
    public async Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest, bool saveToStaging,
        CancellationToken cancellationToken)
    {
        await iiifS3.SaveIIIFToS3(manifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            saveToStaging, cancellationToken);
        
        if (!saveToStaging)
        {
            dbManifest.LastProcessed = DateTime.UtcNow;
        }
    }
    
    private async Task<Manifest> UpsertManifest(Manifest manifest, Models.Database.Collections.Manifest dbManifest, 
        CancellationToken cancellationToken)
    {
        var namedQueryManifest =
            await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);

        var mergedManifest = manifestMerger.ProcessCanvasPaintings(
            manifest,
            namedQueryManifest,
            dbManifest.CanvasPaintings);

        await SaveManifestInStorage(mergedManifest, dbManifest,false, cancellationToken);
        
        return mergedManifest;
    }
}

public interface IManifestStorageManager
{
    /// <summary>
    /// Upserts a final manifest that requires setting items from the staging environment
    /// </summary>
    public Task UpsertManifestFromStagingInStorage(Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Upserts a manifest that requires setting items to the final location directly
    /// </summary>
    public Task<Manifest> UpsertManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a manifest that does not require further processing
    /// </summary>
    public Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        bool saveToStaging, CancellationToken cancellationToken);
}

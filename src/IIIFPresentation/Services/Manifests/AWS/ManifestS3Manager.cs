using AWS.Helpers;
using Core.Helpers;
using Core.Settings;
using Core.Streams;
using DLCS.API;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IOptionsMonitor<BehaviourSettings> behaviour,
    ILogger<ManifestS3Manager> logger) : IManifestStorageManager
{
    public async Task<Manifest> UpsertManifestInStorage(Manifest manifest,
        Models.Database.Collections.Manifest dbManifest,
        string? originalPayload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating manifest {Manifest} in S3", dbManifest.Id);

        var mergedManifest = await UpsertManifest(manifest, dbManifest, originalPayload, cancellationToken);

        return mergedManifest;
    }
    
    public async Task UpsertManifestFromStagingInStorage(Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating manifest {Manifest} in S3", dbManifest.Id);

        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, BucketLocationType.Staging, cancellationToken);
        manifest.ThrowIfNull(nameof(manifest), "Manifest was not found in staging location");

        // Future improvement would be to perform a copy without reading
        string? stagedOriginal = null;
        if (behaviour.CurrentValue.ShouldHaveStoredOriginal(dbManifest.Created))
        {
            var stagedOriginalStream =
                await iiifS3.ReadStreamFromS3(dbManifest, BucketLocationType.OriginalStaging, cancellationToken);
            if (!stagedOriginalStream.IsNull())
            {
                stagedOriginal = await stagedOriginalStream.ReadStreamAsStringAsync(cancellationToken);
            }
        }
        
        await UpsertManifest(manifest!, dbManifest, stagedOriginal, cancellationToken);

        await iiifS3.DeleteIIIFFromS3(dbManifest, true);
    }
    
    public async Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        string? originalPayload, bool saveToStaging, CancellationToken cancellationToken)
    {
        var saveIiif = iiifS3.SaveIIIFToS3(manifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            saveToStaging, cancellationToken);

        if (originalPayload != null)
        {
            var location = saveToStaging ? BucketLocationType.OriginalStaging : BucketLocationType.Original;
            logger.LogDebug("Saving original payload to {Location}", location);
            await iiifS3.SaveToS3(dbManifest, location, originalPayload, cancellationToken);
        }

        await saveIiif;
        
        if (!saveToStaging)
        {
            dbManifest.LastProcessed = DateTime.UtcNow;
        }
    }
    
    private async Task<Manifest> UpsertManifest(Manifest manifest, Models.Database.Collections.Manifest dbManifest, 
        string? originalPayload, CancellationToken cancellationToken)
    {
        var namedQueryManifest =
            await dlcsOrchestratorClient.RetrieveAssetsForManifest(dbManifest.CustomerId, dbManifest.Id,
                cancellationToken);

        var mergedManifest = manifestMerger.MergeManifest(
            manifest,
            namedQueryManifest,
            dbManifest.CanvasPaintings,
            dbManifest.CustomerId,
            dbManifest.Id);

        await SaveManifestInStorage(mergedManifest, dbManifest,originalPayload, false, cancellationToken);
        
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
        string? originalPayload,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves a manifest that does not require further processing
    /// </summary>
    public Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        string? originalPayload,
        bool saveToStaging, CancellationToken cancellationToken);
}

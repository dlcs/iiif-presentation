using AWS.Helpers;
using Core.Settings;
using Core.Streams;
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
    IOptionsMonitor<BehaviourSettings> behaviour,
    ILogger<ManifestS3Manager> logger) : IManifestStorageManager
{
    public async Task<StagedManifest> ReadStagedManifest(Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Reading staged manifest {Manifest} from S3", dbManifest.Id);

        var manifest = await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, BucketLocationType.Staging, cancellationToken);

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

        return new StagedManifest(manifest, stagedOriginal);
    }

    public async Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        string? originalPayload, bool saveToStaging, CancellationToken cancellationToken)
    {
        var saveIiif = iiifS3.SaveIIIFToS3(manifest, dbManifest, pathGenerator.GenerateFlatManifestId(dbManifest),
            saveToStaging, cancellationToken);

        if (originalPayload != null)
        {
            var location = saveToStaging ? BucketLocationType.OriginalStaging : BucketLocationType.Original;
            logger.LogDebug("Saving {ManifestId} original payload to {Location}", manifest.Id, location);
            await iiifS3.SaveToS3(dbManifest, location, originalPayload, cancellationToken);
        }

        await saveIiif;

        if (!saveToStaging)
        {
            dbManifest.LastProcessed = DateTime.UtcNow;
        }
    }

    public Task DeleteStagedManifest(Models.Database.Collections.Manifest dbManifest) =>
        iiifS3.DeleteIIIFFromS3(dbManifest, true);

    public async Task DeleteOriginalPayload(Models.Database.Collections.Manifest dbManifest)
    {
        if (!behaviour.CurrentValue.ShouldHaveStoredOriginal(dbManifest.Created)) return;

        logger.LogDebug("Deleting any stale original payload for manifest {ManifestId}", dbManifest.Id);
        await iiifS3.DeleteFromS3(dbManifest, BucketLocationType.Original);
    }
}

/// <summary>
/// A manifest read from the staging location, along with its stored original payload (if any).
/// </summary>
/// <param name="Manifest">The staged manifest, or null if not found in the staging location.</param>
/// <param name="Original">The stored original request payload, or null if none was stored.</param>
public record StagedManifest(Manifest? Manifest, string? Original);

public interface IManifestStorageManager
{
    /// <summary>
    /// Reads a manifest (and any stored original payload) from the staging location.
    /// </summary>
    public Task<StagedManifest> ReadStagedManifest(Models.Database.Collections.Manifest dbManifest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Upserts provided manifest directly to storage
    /// </summary>
    public Task SaveManifestInStorage(Manifest manifest, Models.Database.Collections.Manifest dbManifest,
        string? originalPayload, bool saveToStaging, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the staged manifest and staged original payload from S3 (e.g. on pipeline submission failure).
    /// </summary>
    public Task DeleteStagedManifest(Models.Database.Collections.Manifest dbManifest);

    /// <summary>
    /// Removes any stored original payload for a manifest (e.g. a stale one left by a prior version).
    /// </summary>
    public Task DeleteOriginalPayload(Models.Database.Collections.Manifest dbManifest);
}

using System.Collections.Immutable;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.Helpers;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace API.Features.Manifest;

public interface IManifestRead
{
    /// <summary>
    /// Attempt to read manifest from storage
    /// </summary>
    public Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId,
        IImmutableSet<Guid> ifNoneMatch, bool pathOnly,
        CancellationToken cancellationToken);
}

public class ManifestReadService(
    PresentationContext dbContext,
    IIIIFS3Service iiifS3,
    DlcsManifestCoordinator dlcsManifestCoordinator,
    IPathGenerator pathGenerator,
    SettingsBasedPathGenerator settingsBasedPathGenerator,
    IOptions<ApiSettings> options,
    ILogger<ManifestReadService> logger) : IManifestRead
{
    public async Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId,
        IImmutableSet<Guid> ifNoneMatch, bool pathOnly, CancellationToken cancellationToken)
    {
        var dbManifest = await dbContext.RetrieveManifestAsync(manifestId, withBatches: true,
            withPipelineJobs: true, cancellationToken: cancellationToken);

        if (dbManifest == null) return FetchEntityResult<PresentationManifest>.NotFound();

        if (ifNoneMatch.Contains(dbManifest.Etag))
            return FetchEntityResult<PresentationManifest>.Matched(dbManifest.Etag);

        var fetchFullPath = ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId,
            dbContext, cancellationToken);

        if (pathOnly)
        {
            return FetchEntityResult<PresentationManifest>.Success(new()
            {
                FullPath = PublicIdGenerator.GetPublicIdFromFullPath(settingsBasedPathGenerator, pathGenerator, customerId, await fetchFullPath)
            }, dbManifest.Etag);
        }

        var getAssets = dlcsManifestCoordinator.GetAssets(customerId, dbManifest, cancellationToken);
        PresentationManifest? manifest = null;
        if (dbManifest.HasFurtherWork())
        {
            manifest = await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, BucketLocationType.Staging, cancellationToken);
            if (manifest == null)
                logger.LogError("Manifest {DbManifestId} has further work pending but can't read from staging", dbManifest.Id);
        }

        // if is not ingesting read from "real" location
        // or if not found in "staging", an error was logged and we fall back to "real"
        manifest ??= await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, BucketLocationType.Default, cancellationToken);

        dbManifest.Hierarchy.Single().FullPath = await fetchFullPath;

        if (manifest == null)
            return FetchEntityResult<PresentationManifest>.Failure(
                "Unable to read and deserialize manifest from storage");

        var assets = await getAssets;

        // If the DLCS lookup failed, assets will be null (error already logged in DlcsManifestCoordinator).
        // Return the manifest without manifest-level adjuncts rather than failing the GET.
        if (assets != null) manifest.SetManifestLevelAdjuncts(assets, customerId, dbManifest.Id);

        manifest = manifest.SetGeneratedFields(dbManifest, pathGenerator, settingsBasedPathGenerator, assets,
            m => Enumerable.Single(m.Hierarchy!, h => h.Canonical), options.Value.FinishedPipelinesLimit);

        Guid? etag = dbManifest.Etag;
        if (dbManifest.HasFurtherWork())
        {
            manifest.CurrentlyIngesting = true;
            etag = null;
        }

        return FetchEntityResult<PresentationManifest>.Success(manifest, etag);
    }

}

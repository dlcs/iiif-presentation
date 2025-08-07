using System.Collections.Immutable;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using AWS.Helpers;
using DLCS.API;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;

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
    IDlcsApiClient dlcsApiClient,
    DlcsManifestCoordinator dlcsManifestCoordinator,
    IPathGenerator pathGenerator,
    ILogger<ManifestReadService> logger) : IManifestRead
{
    public async Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId,
        IImmutableSet<Guid> ifNoneMatch, bool pathOnly, CancellationToken cancellationToken)
    {
        var dbManifest = await dbContext.RetrieveManifestAsync(customerId, manifestId, withBatches: true,
            cancellationToken: cancellationToken);

        if (dbManifest == null) return FetchEntityResult<PresentationManifest>.NotFound();

        if (ifNoneMatch.Contains(dbManifest.Etag))
            return FetchEntityResult<PresentationManifest>.Matched(dbManifest.Etag);

        var fetchFullPath = ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId,
            dbContext, cancellationToken);

        if (pathOnly)
        {
            return FetchEntityResult<PresentationManifest>.Success(new()
            {
                FullPath = pathGenerator.GenerateHierarchicalFromFullPath(customerId, await fetchFullPath)
            }, dbManifest.Etag);
        }

        var getAssets = dlcsManifestCoordinator.GetAssets(customerId, dbManifest, cancellationToken);
        PresentationManifest? manifest = null;
        if (dbManifest.IsIngesting())
        {
            manifest = await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, true, cancellationToken);
            if (manifest == null)
                logger.LogError("Manifest {DbManifestId} IsIngesting but can't read from staging", dbManifest.Id);
        }

        // if is not ingesting read from "real" location
        // or if not found in "staging", an error was logged and we fall back to "real"
        manifest ??= await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, false, cancellationToken);


        dbManifest.Hierarchy.Single().FullPath = await fetchFullPath;

        if (manifest == null)
            return FetchEntityResult<PresentationManifest>.Failure(
                "Unable to read and deserialize manifest from storage");

        var assets = await getAssets;
        manifest = manifest.SetGeneratedFields(dbManifest, pathGenerator, assets,
            m => Enumerable.Single<Hierarchy>(m.Hierarchy!, h => h.Canonical));

        Guid? etag = dbManifest.Etag;
        if (dbManifest.IsIngesting())
        {
            manifest.CurrentlyIngesting = true;
            etag = null;
        }

        return FetchEntityResult<PresentationManifest>.Success(manifest, etag);
    }
}

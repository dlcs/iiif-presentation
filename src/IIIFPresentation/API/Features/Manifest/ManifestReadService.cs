using System.Collections.Immutable;
using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using AWS.Helpers;
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
    ILogger<ManifestReadService> logger) : IManifestRead
{
    public async Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId,
        IImmutableSet<Guid> ifNoneMatch, bool pathOnly, CancellationToken cancellationToken)
    {
        var dbManifest = await dbContext.RetrieveManifestAsync(manifestId, withBatches: true,
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
                FullPath = PublicIdGenerator.GetPublicIdFromFullPath(settingsBasedPathGenerator, pathGenerator, customerId, await fetchFullPath)
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
        manifest = manifest.SetGeneratedFields(dbManifest, pathGenerator, settingsBasedPathGenerator, assets,
            m => Enumerable.Single(m.Hierarchy!, h => h.Canonical));

        // If the DLCS lookup failed, assets will be null (error already logged in DlcsManifestCoordinator).
        // Return the manifest without manifest-level adjuncts rather than failing the GET.
        if (assets != null) SetManifestLevelAdjuncts(manifest, assets, customerId, dbManifest.Id);

        Guid? etag = dbManifest.Etag;
        if (dbManifest.IsIngesting())
        {
            manifest.CurrentlyIngesting = true;
            etag = null;
        }

        return FetchEntityResult<PresentationManifest>.Success(manifest, etag);
    }

    private static void SetManifestLevelAdjuncts(PresentationManifest manifest,
        Dictionary<string, JObject> assets, int customerId, string manifestId)
    {
        var stubAssetId = ResourceAdjunctInteractions.GetResourceStubAssetId(customerId, manifestId);
        var stubAsset = assets.Values.FirstOrDefault(a => a[AssetProperties.Id]?.Value<string>() == stubAssetId.Asset);
        if (stubAsset?[AssetProperties.Adjuncts] is JArray adjunctsArray)
        {
            manifest.Adjuncts = adjunctsArray.OfType<JObject>()
                .Select(a => { a.Remove(AssetProperties.Asset); return a; })
                .ToList();
        }
    }
}

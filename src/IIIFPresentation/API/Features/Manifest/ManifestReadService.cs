using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using AWS.Helpers;
using DLCS.API;
using DLCS.Exceptions;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.Database.General;
using Newtonsoft.Json.Linq;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Manifest;

public interface IManifestRead
{
    /// <summary>
    /// Get a lookup of full Asset URI : <see cref="JObject"/> for all assets in given manifest
    /// </summary>
    /// <returns></returns>
    Task<Dictionary<string, JObject>?> GetAssets(int customerId, DbManifest? dbManifest,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to read manifest from storage
    /// </summary>
    public Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId, bool pathOnly,
        CancellationToken cancellationToken);
}

public class ManifestReadService(
    PresentationContext dbContext,
    IIIFS3Service iiifS3,
    IDlcsApiClient dlcsApiClient,
    IPathGenerator pathGenerator,
    ILogger<ManifestReadService> logger) : IManifestRead
{
    public async Task<Dictionary<string, JObject>?> GetAssets(int customerId, Models.Database.Collections.Manifest? dbManifest,
        CancellationToken cancellationToken)
    {
        var assetIds = dbManifest?.CanvasPaintings?.Select(cp => cp.AssetId?.ToString())
            .OfType<string>().ToArray();

        if (assetIds == null) return null;

        try
        {
            var assets = await dlcsApiClient.GetCustomerImages(customerId, assetIds, cancellationToken);

            return assets.Select(a => (asset: a,
                    id: a.TryGetValue("@id", out var value) && value.Type == JTokenType.String
                        ? value.Value<string>()
                        : null))
                .Where(tuple => tuple.id is { Length: > 0 })
                .ToDictionary(tuple => tuple.id!, tuple => tuple.asset);
        }
        catch (DlcsException dlcsException)
        {
            logger.LogError(dlcsException, "Error retrieving selected asset details for Customer {CustomerId}",
                customerId);

            return null;
        }
    }

    public async Task<FetchEntityResult<PresentationManifest>> GetManifest(int customerId, string manifestId, bool pathOnly, CancellationToken cancellationToken)
    {
        var dbManifest = await dbContext.RetrieveManifestAsync(customerId, manifestId, withBatches: true,
            cancellationToken: cancellationToken);

        if (dbManifest == null) return FetchEntityResult<PresentationManifest>.NotFound();

        var fetchFullPath = ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId,
            dbContext, cancellationToken);

        if (pathOnly)
        {
            return FetchEntityResult<PresentationManifest>.Success(new()
            {
                FullPath = pathGenerator.GenerateHierarchicalFromFullPath(customerId, await fetchFullPath)
            });
        }

        var getAssets = GetAssets(customerId, dbManifest, cancellationToken);
        var manifest = await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, cancellationToken);
        dbManifest.Hierarchy.Single().FullPath = await fetchFullPath;

        // PK: Will this even happen? Should we log or even throw here?
        if (manifest == null)
            return FetchEntityResult<PresentationManifest>.Failure(
                "Unable to read and deserialize manifest from storage");

        var assets = await getAssets;
        manifest = manifest.SetGeneratedFields(dbManifest, pathGenerator, assets,
            m => Enumerable.Single<Hierarchy>(m.Hierarchy!, h => h.Canonical));

        if (dbManifest.IsIngesting())
        {
            manifest.CurrentlyIngesting = true;
        }

        return FetchEntityResult<PresentationManifest>.Success(manifest);
    }
}

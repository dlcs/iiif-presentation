using System.Diagnostics;
using System.Net;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using Core;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using JsonDiffPatchDotNet;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.DLCS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Repository;
using CanvasPainting = Models.Database.CanvasPainting;
using EntityResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class DlcsInteractionResult(EntityResult? error, int? spaceId, bool canBeBuiltUpfront = false, bool onlySpace = false)
{
    public EntityResult? Error { get; } = error;
    public int? SpaceId { get; } = spaceId;
    
    public bool CanBeBuiltUpfront { get; } = canBeBuiltUpfront;
    
    public bool OnlySpace { get; } = onlySpace;
    
    public static readonly DlcsInteractionResult NoInteraction = new(null, null);
        
    public static DlcsInteractionResult Fail(EntityResult error) => new(error, null);
}

public class DlcsManifestCoordinator(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    IManagedAssetResultFinder knownAssetChecker,
    ILogger<DlcsManifestCoordinator> logger)
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
                    id: a.TryGetValue(AssetProperties.FullId, out var value) && value.Type == JTokenType.String
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
    
    /// <summary>
    /// Carry out any required interactions with DLCS for given <see cref="WriteManifestRequest"/>, this can include
    /// creating a space and/or creating DLCS batches
    /// </summary>
    /// <returns>Any errors encountered and new Manifest SpaceId if created</returns>
    public async Task<DlcsInteractionResult> HandleDlcsInteractions(
        WriteManifestRequest request,
        string manifestId, 
        Models.Database.Collections.Manifest? dbManifest = null, 
        CancellationToken cancellationToken = default)
    {
        // NOTE - this must always happen before handing off to canvasPaintingResolve
        var assets = GetAssetJObjectList(request);

        if (!request.CreateSpace && assets.Count <= 0)
        {
            logger.LogDebug("No assets or space required, DLCS integrations not required");
            return DlcsInteractionResult.NoInteraction;
        }

        if (assets.Any(a => !a.HasValues))
        {
            return DlcsInteractionResult.Fail(ErrorHelper.CouldNotRetrieveAssetId<PresentationManifest>());
        }

        int? spaceId = null;
        var assetsWithoutSpaces = assets.Where(a => !a.TryGetValue(AssetProperties.Space, out _)).ToArray();
        var createdSpace = false;
        
        if (request.CreateSpace || assetsWithoutSpaces.Length > 0)
        {
            if (dbManifest?.SpaceId != null)
            {
                spaceId = dbManifest.SpaceId.Value;
            }
            else
            {
                // Either you want a space or we detected you need a space regardless
                spaceId = await CreateSpace(request.CustomerId, manifestId, cancellationToken);
                if (!spaceId.HasValue)
                {
                    return DlcsInteractionResult.Fail(
                        ErrorHelper.DlcsError<PresentationManifest>("Error creating DLCS space"));
                }

                // you wanted a space, and there are no assets, so no further work required
                if (assets.Count == 0)
                {
                    return new DlcsInteractionResult(null, spaceId, onlySpace: true);
                }
                
                createdSpace = true;
            }

            foreach (var asset in assetsWithoutSpaces)
                asset.Add(AssetProperties.Space, spaceId.Value);
            
        }

        return await UpdateDlcsWithAssets(request, manifestId, dbManifest, assets, spaceId, createdSpace, cancellationToken);
    }

    private async Task<DlcsInteractionResult> UpdateDlcsWithAssets(WriteManifestRequest request, string manifestId, 
        Models.Database.Collections.Manifest? dbManifest, List<JObject> assets, int? spaceId, bool spaceCreated,
        CancellationToken cancellationToken)
    {
        var dlcsInteractionRequests = await knownAssetChecker.FindAssetsThatRequireAdditionalWork(
            request.PresentationManifest, dbManifest, spaceId, spaceCreated, request.CustomerId, cancellationToken);

        MarkAssetsAsIngesting(request,
            dlcsInteractionRequests.Where(d => d.Ingest != IngestType.NoIngest).Select(d => d.AssetId));
        
        // create batches for assets
        var batchError = await CreateBatches(request.CustomerId, manifestId,
            dlcsInteractionRequests.Where(d => d.Ingest != IngestType.NoIngest).ToList(), cancellationToken);
        
        if (batchError != null)  return new DlcsInteractionResult(batchError, spaceId);
        
        // then update existing assets in another manifest with the current manifest id
        await UpdateAssetsWithManifestId(request, manifestId,
            dlcsInteractionRequests.Where(d => d.Patch).Select(d => d.AssetId).ToList(), cancellationToken);

        await RemoveUnusedAssets(dbManifest, assets, cancellationToken);

        var canBeBuiltUpfront = dlcsInteractionRequests.All(d => d.Ingest == IngestType.NoIngest) && assets.Count > 0;
        return new DlcsInteractionResult(batchError, spaceId, canBeBuiltUpfront);
    }

    /// <summary>
    /// Makes sure that all assets which have been ingested into the DLCS are marked as ingesting
    /// in the <see cref="Models.API.Manifest.CanvasPainting"/>> record
    /// </summary>
    private static void MarkAssetsAsIngesting(WriteManifestRequest request,
        IEnumerable<AssetId> assetToMarkAsIngesting)
    {
        foreach (var paintedResource in assetToMarkAsIngesting.SelectMany(untrackedAsset =>
                     request.PresentationManifest.PaintedResources.Where(pr =>
                         pr.Asset.GetAssetId(request.CustomerId) == untrackedAsset)))
        {
            paintedResource.CanvasPainting ??= new Models.API.Manifest.CanvasPainting();
            paintedResource.CanvasPainting.Ingesting = true;
        }
    }

    private async Task UpdateAssetsWithManifestId(WriteManifestRequest request, string manifestId,
        List<AssetId> assetsToUpdate, CancellationToken cancellationToken)
    {
        if (assetsToUpdate.Count != 0)
        {
            await dlcsApiClient.UpdateAssetManifest(request.CustomerId,
                assetsToUpdate.Select(x => x.ToString()).ToList(), OperationType.Add, [manifestId],
                cancellationToken);
        }
    }

    private async Task RemoveUnusedAssets(Models.Database.Collections.Manifest? dbManifest, List<JObject> assets, 
        CancellationToken cancellationToken)
    {
        if (dbManifest == null) return;

        var canvasPaintingsInDatabase = (dbManifest.CanvasPaintings ?? []).Where(cp => cp.AssetId != null);
        var assetIds = assets.Select(a => a.GetAssetId(dbManifest.CustomerId));

        var assetsToRemove = canvasPaintingsInDatabase.Where(cp => assetIds.All(a => a != cp.AssetId)).ToList();

        if (assetsToRemove.Any())
        {
            await RemoveManifestsFromAssets(dbManifest, assetsToRemove, cancellationToken);
        }
    }

    private async Task RemoveManifestsFromAssets(Models.Database.Collections.Manifest dbManifest, 
        IEnumerable<CanvasPainting> canvasPaintingsToRemove, CancellationToken cancellationToken) =>
        await dlcsApiClient.UpdateAssetManifest(dbManifest.CustomerId,
        canvasPaintingsToRemove.Select(cp => cp.AssetId?.ToString()).ToList(), OperationType.Remove,
    [dbManifest.Id], cancellationToken);

    private static List<JObject> GetAssetJObjectList(WriteManifestRequest request) =>
        request.PresentationManifest.PaintedResources?
            .Select(p => p.Asset)
            .OfType<JObject>()
            .ToList() ?? [];
    
    private async Task<int?> CreateSpace(int customerId, string manifestId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating new space for customer {Customer}, Manifest {ManifestId}", customerId, manifestId);
        var newSpace =
            await dlcsApiClient.CreateSpace(customerId, ManifestX.GetDefaultSpaceName(manifestId), cancellationToken);
        return newSpace.Id;
    }

    private async Task<EntityResult?> CreateBatches(int customerId, string manifestId, 
        List<DlcsInteractionRequest> dlcsInteractionRequests, CancellationToken cancellationToken)
    {
        if (dlcsInteractionRequests.Count == 0) return null;

        var assets = new Dictionary<AssetId, JObject>();
        
        foreach (var dlcsInteractionRequest in dlcsInteractionRequests)
        {
            switch (dlcsInteractionRequest.Ingest)
            {
                case IngestType.NoManifestId:
                {
                    if (dlcsInteractionRequest.Asset.ContainsKey(AssetProperties.Manifests))
                    {
                        dlcsInteractionRequest.Asset[AssetProperties.Manifests] = null;
                    }
                    break;
                }
                case IngestType.ManifestId:
                    dlcsInteractionRequest.Asset[AssetProperties.Manifests] = new JArray(new List<string> { manifestId });
                    break;
            }

            if (!assets.TryAdd(dlcsInteractionRequest.AssetId, dlcsInteractionRequest.Asset))
            {
                logger.LogDebug("Asset {AssetId} has been specified multiple times, validating they match", dlcsInteractionRequest.AssetId);
                var assetInDictionary = assets[dlcsInteractionRequest.AssetId];
                
                if (!JToken.DeepEquals(assetInDictionary, dlcsInteractionRequest.Asset))
                {
                    var jsonDiffPatch = new JsonDiffPatch();
                    var diff = jsonDiffPatch.Diff(assetInDictionary, dlcsInteractionRequest.Asset);
                    
                    return EntityResult.Failure(
                        $"Asset {dlcsInteractionRequest.AssetId} is specified multiple times, but has conflicting data - diff: {JsonConvert.SerializeObject(diff)}",
                        ModifyCollectionType.AssetsDoNotMatch, WriteResult.BadRequest);
                }
            }
        }
        
        try
        {
            var batches = await dlcsApiClient.IngestAssets(customerId,
                assets.Values.ToList(),
                cancellationToken);

            await batches.AddBatchesToDatabase(customerId, manifestId, dbContext, cancellationToken);
            return null;
        }
        catch (DlcsException exception)
        {
            logger.LogError(exception, "Error creating batch request for customer {CustomerId}, manifest {ManifestId}",
                customerId, manifestId);
            return EntityResult.Failure(exception.Message, ModifyCollectionType.DlcsException,
                ErrorStatusCodeToWriteResult(exception.StatusCode));
        }
    }

    private WriteResult ErrorStatusCodeToWriteResult(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => WriteResult.BadRequest,
            HttpStatusCode.NotFound => WriteResult.NotFound,
            HttpStatusCode.Conflict => WriteResult.Conflict,
            _ => WriteResult.Error
        };
    }
}

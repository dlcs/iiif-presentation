using System.Net;
using API.Features.Common.Helpers;
using API.Features.Manifest.Exceptions;
using API.Helpers;
using API.Infrastructure.Helpers;
using Core;
using Core.Exceptions;
using Core.Helpers;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using JsonDiffPatchDotNet;
using Models.API.General;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Repository;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using EntityResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class DlcsInteractionResult(EntityResult? error, int? spaceId, bool canBeBuiltUpfront = false, bool onlySpace = false, 
    List<AssetId>? ingestedAssets = null)
{
    public EntityResult? Error { get; } = error;
    public int? SpaceId { get; } = spaceId;
    
    public bool CanBeBuiltUpfront { get; } = canBeBuiltUpfront;
    
    public bool OnlySpace { get; } = onlySpace;

    public List<AssetId>? IngestedAssets { get; } = ingestedAssets;
    
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
        if (dbManifest == null) return null;

        try
        {
            var assets = await dlcsApiClient.GetCustomerImages(customerId, dbManifest.Id, cancellationToken);

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
    /// <param name="request">The full request to get a manifest</param>
    /// <param name="manifestId">The id of the manifest</param>
    /// <param name="previousManifestAssetIds">every asset id in the previous manifest</param>
    /// <param name="dbManifest">The current manifest in the database</param>
    /// <param name="itemCanvasPaintingsWithAssets">canvas paintings from items that contain asset ids</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Any errors encountered and new Manifest SpaceId if created</returns>
    public async Task<DlcsInteractionResult> HandleDlcsInteractions(WriteManifestRequest request,
        string manifestId,
        List<AssetId>? previousManifestAssetIds = null,
        Models.Database.Collections.Manifest? dbManifest = null,
        List<InterimCanvasPainting>? itemCanvasPaintingsWithAssets = null,
        CancellationToken cancellationToken = default)
    {
        var errorFromItems = await HandleItemsDlcsInteractions(request, manifestId, previousManifestAssetIds,
            itemCanvasPaintingsWithAssets, cancellationToken);
        if (errorFromItems != null) return errorFromItems;

        return await HandlePaintedResourceDlcsInteractions(request, manifestId,
            itemCanvasPaintingsWithAssets?.GetAssetIds() ?? [], previousManifestAssetIds, dbManifest?.SpaceId,
            cancellationToken);
    }

    private async Task<DlcsInteractionResult?> HandleItemsDlcsInteractions(WriteManifestRequest request, string manifestId, List<AssetId>? existingAssetIds,
        List<InterimCanvasPainting>? itemCanvasPaintingsWithAssets, CancellationToken cancellationToken)
    {
        try
        {
            var assetsToUpdateWithManifestId = await knownAssetChecker.CheckAssetsFromItemsExist(itemCanvasPaintingsWithAssets, request.CustomerId, existingAssetIds,
                cancellationToken);
            
            await UpdateAssetsWithManifestId(request, manifestId,
                assetsToUpdateWithManifestId, cancellationToken);
        }
        catch (PresentationException presentationException)
        {
            logger.LogError(presentationException, "Error checking for the existence of assets");

            return DlcsInteractionResult.Fail(UpsertErrorHelper.PaintableAssetError<PresentationManifest>(presentationException.Message));
        }

        return null;
    }

    private async Task<DlcsInteractionResult> HandlePaintedResourceDlcsInteractions(
        WriteManifestRequest request,
        string manifestId, 
        List<AssetId> assetsFromItems,
        List<AssetId>? previousManifestAssetIds = null,
        int? manifestSpaceId = null,
        CancellationToken cancellationToken = default)
    {
        var assets = GetAssetJObjectList(request.PresentationManifest.PaintedResources);

        if (!request.CreateSpace && assets.Count <= 0 && previousManifestAssetIds.IsNullOrEmpty())
        {
            logger.LogDebug("No assets or space required, DLCS integrations not required");
            return DlcsInteractionResult.NoInteraction;
        }

        int? spaceId = null;
        var assetsWithoutSpaces = assets.Where(a => !a.TryGetValue(AssetProperties.Space, out _)).ToArray();
        var createdSpace = false;
        
        if (request.CreateSpace || assetsWithoutSpaces.Length > 0)
        {
            if (manifestSpaceId != null)
            {
                spaceId = manifestSpaceId.Value;
            }
            else
            {
                // Either you want a space or we detected you need a space regardless
                spaceId = await CreateSpace(request.CustomerId, manifestId, cancellationToken);
                if (!spaceId.HasValue)
                {
                    return DlcsInteractionResult.Fail(
                        UpsertErrorHelper.DlcsError<PresentationManifest>("Error creating DLCS space"));
                }

                // you wanted a space, and there are no assets, so no further work required
                if (assets.Count == 0)
                {
                    return new DlcsInteractionResult(null, spaceId, onlySpace: true);
                }
                
                createdSpace = true;
            }

            SpaceHelper.UpdateAssets(assetsWithoutSpaces, spaceId.Value);
            
        }

        return await UpdateDlcsWithAssets(request, manifestId, previousManifestAssetIds, assets, assetsFromItems, spaceId,
            createdSpace, cancellationToken);
    }

    private async Task<DlcsInteractionResult> UpdateDlcsWithAssets(WriteManifestRequest request, string manifestId, 
        List<AssetId>? previousManifestAssetIds, List<JObject> assets, List<AssetId> assetsFromItems, int? spaceId, bool spaceCreated, 
        CancellationToken cancellationToken)
    {
        List<DlcsInteractionRequest> dlcsInteractionRequests;
        
        try
        {
            dlcsInteractionRequests = await knownAssetChecker.FindAssetsThatRequireAdditionalWork(
                request.PresentationManifest, previousManifestAssetIds, spaceId, spaceCreated, request.CustomerId,
                cancellationToken);
        }
        catch (AssetIdException assetIdException)
        {
            logger.LogError(assetIdException, "Error parsing  DLCS asset that requires more work for manifest {ManifestId}", manifestId);

            var error = $"Error parsing the asset id from an attached asset - {assetIdException.Message}";

            if (assetIdException.Data.Contains(ExceptionDataType.CanvasPaintingId))
            {
                error += $" for canvas painting id '{assetIdException.Data[ExceptionDataType.CanvasPaintingId]}'";
            }
            
            return new DlcsInteractionResult(EntityResult.Failure(error, ModifyCollectionType.AssetError,
                WriteResult.BadRequest), spaceId);
        }

        var assetsToIngest = dlcsInteractionRequests.Where(d => d.Ingest != IngestType.NoIngest).ToList();
        // create batches for assets
        var batchError = await CreateBatches(request.CustomerId, manifestId, assetsToIngest.ToList(), cancellationToken);
        
        if (batchError != null)  return new DlcsInteractionResult(batchError, spaceId);
        
        // then update existing assets in another manifest with the current manifest id
        await UpdateAssetsWithManifestId(request, manifestId,
            dlcsInteractionRequests.Where(d => d.Patch).Select(d => d.AssetId).ToList(), cancellationToken);

        await RemoveUnusedAssets(previousManifestAssetIds, manifestId, request.CustomerId, assets, assetsFromItems, cancellationToken);

        var ingestedAssets = dlcsInteractionRequests.Where(d => d.Ingest != IngestType.NoIngest).Select(d => d.AssetId)
            .ToList();
        var canBeBuiltUpfront = dlcsInteractionRequests.Count == 0 || 
                                (dlcsInteractionRequests.All(d => d.Ingest == IngestType.NoIngest) && assets.Count > 0);
        return new DlcsInteractionResult(batchError, spaceId, canBeBuiltUpfront, ingestedAssets: ingestedAssets);
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

    private async Task RemoveUnusedAssets(List<AssetId>? previousManifestAssetIds, string dbManifestId, int customerId, List<JObject> assets, List<AssetId> assetsFromItems,
        CancellationToken cancellationToken)
    {
        if (previousManifestAssetIds == null) return;
        var assetIds = assets.Select(a => a.GetAssetId(customerId));

        var assetsToRemove = previousManifestAssetIds.Where(e => assetIds.All(a => a != e) && assetsFromItems.All(a => a != e))
            .ToList();

        if (assetsToRemove.Any())
        {
            await RemoveManifestsFromAssets(dbManifestId, customerId, assetsToRemove, cancellationToken);
        }
    }

    private async Task RemoveManifestsFromAssets(string dbManifestId, int customerId, 
        IEnumerable<AssetId> assetsToRemove, CancellationToken cancellationToken) =>
        await dlcsApiClient.UpdateAssetManifest(customerId,
            assetsToRemove.Select(cp => cp.ToString()).ToList(), OperationType.Remove,
    [dbManifestId], cancellationToken);

    private static List<JObject> GetAssetJObjectList(List<PaintedResource>? paintedResources) =>
        paintedResources?
            .Select(p => p.Asset)
            .OfType<JObject>()
            .ToList() ?? [];

    private async Task<int?> CreateSpace(int customerId, string manifestId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating new space for customer {Customer}, Manifest {ManifestId}", customerId, manifestId);
        var newSpace =
            await dlcsApiClient.CreateSpace(customerId, Models.Database.Collections.ManifestX.GetDefaultSpaceName(manifestId), cancellationToken);
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

            // If can add, move to next one
            if (assets.TryAdd(dlcsInteractionRequest.AssetId, dlcsInteractionRequest.Asset)) continue;
            
            // else already specified
            logger.LogDebug("Asset {AssetId} has been specified multiple times, validating they match", dlcsInteractionRequest.AssetId);
            var assetInDictionary = assets[dlcsInteractionRequest.AssetId];

            // if specified with the same data, we can ignore and continue
            if (JToken.DeepEquals(assetInDictionary, dlcsInteractionRequest.Asset)) continue;
            
            // else report failure with details
            var jsonDiffPatch = new JsonDiffPatch();
            var diff = jsonDiffPatch.Diff(assetInDictionary, dlcsInteractionRequest.Asset);
                    
            return EntityResult.Failure(
                $"Asset {dlcsInteractionRequest.AssetId} is specified multiple times, but has conflicting data - diff: {JsonConvert.SerializeObject(diff)}",
                ModifyCollectionType.AssetsDoNotMatch, WriteResult.BadRequest);
        }
        
        // `assets` now contain all the assets that should be ingested by DLCS
        
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

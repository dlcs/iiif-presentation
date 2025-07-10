using System.Net;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using Core;
using Core.Helpers;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using CanvasPainting = Models.Database.CanvasPainting;
using EntityResult = API.Infrastructure.Requests.ModifyEntityResult<Models.API.Manifest.PresentationManifest, Models.API.General.ModifyCollectionType>;

namespace API.Features.Manifest;

public class DlcsInteractionResult(EntityResult? error, int? spaceId, bool canBeBuiltUpfront = false)
{
    public EntityResult? Error { get; } = error;
    public int? SpaceId { get; } = spaceId;
    
    public bool CanBeBuiltUpfront { get; } = canBeBuiltUpfront;
    
    public static readonly DlcsInteractionResult NoInteraction = new(null, null);
        
    public static DlcsInteractionResult Fail(EntityResult error) => new(error, null);
}

public class DlcsManifestCoordinator(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    ILogger<DlcsManifestCoordinator> logger)
{
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
                    return DlcsInteractionResult.Fail(ErrorHelper.ErrorCreatingSpace<PresentationManifest>());
                }
            }

            foreach (var asset in assetsWithoutSpaces)
                asset.Add(AssetProperties.Space, spaceId.Value);
            
        }

        return await UpdateDlcsWithAssets(request, manifestId, dbManifest, assets, spaceId, cancellationToken);
    }

    private async Task<DlcsInteractionResult> UpdateDlcsWithAssets(WriteManifestRequest request, string manifestId, 
        Models.Database.Collections.Manifest? dbManifest, List<JObject> assets, int? spaceId,
        CancellationToken cancellationToken)
    {
        var checkedAssets =
            await FindAssetsThatRequireAdditionalWork(assets, request.PresentationManifest, dbManifest, request.CustomerId,
                cancellationToken);

        SetUntrackedAndReingestingAssetsToIngesting(request, checkedAssets.UntrackedAssets,
            checkedAssets.ReingestedAssetsInAnotherManifest);
        
        // first create batches for assets being reingested in another manifest
        var reingestAssetsInAnotherManifestBatchErrors = await CreateBatches(request.CustomerId, manifestId,
            checkedAssets.ReingestedAssetsInAnotherManifest, false, cancellationToken);

        if (reingestAssetsInAnotherManifestBatchErrors?.Error != null)
        {
            return new DlcsInteractionResult(reingestAssetsInAnotherManifestBatchErrors, spaceId);
        }
        
        // then update existing assets in another manifest with the current manifest id
        await UpdateAssetsWithManifestId(request, manifestId, checkedAssets.DlcsAssetIds, cancellationToken);

        await RemoveUnusedAssets(dbManifest, assets, cancellationToken);

        // create batches for untracked assets
        var batchError = await CreateBatches(request.CustomerId, manifestId, checkedAssets.UntrackedAssets, true,
            cancellationToken);

        var canBeBuiltUpfront = checkedAssets.UntrackedAssets.Count == 0 && assets.Count > 0 &&
                                checkedAssets.ReingestedAssetsInAnotherManifest.Count == 0;
        return new DlcsInteractionResult(batchError, spaceId, canBeBuiltUpfront);
    }

    /// <summary>
    /// Makes sure that all assets which have been ingested into the DLCS are marked as ingesting
    /// in the <see cref="Models.API.Manifest.CanvasPainting"/>> record
    /// </summary>
    private static void SetUntrackedAndReingestingAssetsToIngesting(WriteManifestRequest request,
        List<JObject> untrackedAssets, List<JObject> reingestingAssets)
    {
        var combinedAssets = untrackedAssets.Union(reingestingAssets);
        
        foreach (var paintedResource in combinedAssets.SelectMany(untrackedAsset =>
                     request.PresentationManifest.PaintedResources.Where(pr =>
                         pr.Asset.GetAssetId(request.CustomerId) == untrackedAsset.GetAssetId(request.CustomerId))))
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

        var canvasPaintingsInDatabase = dbManifest.CanvasPaintings ?? [];
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
    
    private async Task<AssetsWithAdditionalWork> FindAssetsThatRequireAdditionalWork(
        List<JObject> assets, PresentationManifest presentationManifest, Models.Database.Collections.Manifest? dbManifest, 
        int customerId, CancellationToken cancellationToken = default)
    {
        var assetIdsWithReingestion = presentationManifest.PaintedResources?.Select(pr =>
                new AssetIdsWithReingestion(pr.Asset!.GetAssetId(customerId), pr.Reingest))
            .ToList() ?? [];

        var (assetsInDatabase, trackedAssetsToReingest) = RetrieveTrackedAssets(customerId, assetIdsWithReingestion);

        var assetsTrackedElsewhere = assetsInDatabase.Where(a => a.ManifestId != dbManifest?.Id)
            .Select(a => a.AssetId!).Distinct().ToList();

        // all assets are tracked and not to be reingested
        if (assetsInDatabase.Count == assetIdsWithReingestion.Count && assetsTrackedElsewhere.IsNullOrEmpty() && 
            trackedAssetsToReingest.IsNullOrEmpty())
        {
            logger.LogTrace("All assets do not require reingestion and are tracked in the database for {ManifestId}",
                dbManifest?.Id ?? "new manifest");
            return new AssetsWithAdditionalWork([], [], []);
        }

        var allAssetsTracked = await CheckDlcsForTrackedAssets(assets, dbManifest, customerId, cancellationToken,
            assetIdsWithReingestion, assetsInDatabase, assetsTrackedElsewhere, trackedAssetsToReingest);

        if (allAssetsTracked != null)
        {
            return allAssetsTracked;
        }

        var assetsRequiringIngestion = GetAssetsRequiringAdditionalWork(assets, assetsInDatabase, assetsTrackedElsewhere,
            trackedAssetsToReingest, dbManifest);

        return new AssetsWithAdditionalWork(assetsRequiringIngestion.UntrackedAssets, assetsTrackedElsewhere,
            assetsRequiringIngestion.ReingestedassetsInAnotherManifest);
    }

    private async Task<AssetsWithAdditionalWork?> CheckDlcsForTrackedAssets(List<JObject> assets, Models.Database.Collections.Manifest? dbManifest, int customerId,
        CancellationToken cancellationToken, List<AssetIdsWithReingestion> assetIdsWithReingestion, List<CanvasPainting> assetsInDatabase,
        List<AssetId> assetsTrackedElsewhere, List<CanvasPainting> trackedAssetsToReingest)
    {
        // no need to check the DLCS for assets that need to be reingested as we're going to reingest anyway
        var assetsToCheckDlcs = assetIdsWithReingestion
            .Where(a => assetsInDatabase.All(b => b.AssetId != a.AssetId)  && !a.Reingest).ToList();

        IList<JObject> dlcsAssets = [];
        
        try
        {
            dlcsAssets = await dlcsApiClient.GetCustomerImages(customerId,
                assetsToCheckDlcs.Select(a => a.AssetId.ToString()).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve DLCS assets");
        }
        
        assetsTrackedElsewhere.AddRange(dlcsAssets.Select(a => a.GetAssetId(customerId)));

        if (assetsTrackedElsewhere.Count == assets.Count && !trackedAssetsToReingest.Any())
        {
            logger.LogTrace("All assets tracked for {ManifestId}", dbManifest?.Id ?? "new manifest");
            return new AssetsWithAdditionalWork([], assetsTrackedElsewhere, []);
        }

        return null;
    }

    private (List<CanvasPainting> AssetsInDatabase, List<CanvasPainting> TrackedAssetsToReingest) RetrieveTrackedAssets(
        int customerId, List<AssetIdsWithReingestion> assetIdsWithReingestion)
    {
        List<CanvasPainting> assetsInDatabase = [];
        List<CanvasPainting> trackedAssetsToReingest = [];

        foreach (var canvasPainting in dbContext.CanvasPaintings.Where(cp => cp.CustomerId == customerId))
        {
            if (canvasPainting.AssetId == null) continue;
            
            var trackedAssetId = assetIdsWithReingestion.FirstOrDefault(a => a.AssetId == canvasPainting.AssetId);

            if (trackedAssetId == null) continue;
            if (trackedAssetId.Reingest)
            {
                trackedAssetsToReingest.Add(canvasPainting);
            }
            
            assetsInDatabase.Add(canvasPainting);
        }

        return (assetsInDatabase, trackedAssetsToReingest);
    }


    private static (List<JObject> UntrackedAssets, List<JObject> ReingestedassetsInAnotherManifest) GetAssetsRequiringAdditionalWork(
        List<JObject> payloadAssets, List<CanvasPainting> assetsInDatabase, List<AssetId> dlcsAssetIds, 
        List<CanvasPainting> assetsToReingest,  Models.Database.Collections.Manifest? dbManifest)
    {
        var knownAssets = dlcsAssetIds.Union(assetsInDatabase.Select(a => a.AssetId));
        
        // get all the assets that aren't known assets and don't require reingesting
        var untrackedAssets = payloadAssets.Where(a =>
            !knownAssets.Any(b =>
                b.Asset == a.GetRequiredValue<string>(AssetProperties.Id) &&
                b.Space == a.GetRequiredValue<int>(AssetProperties.Space)) &&
            assetsToReingest.All(c => c.AssetId != a.GetAssetId(c.CustomerId))).ToList();

        var assetsRequiringReingestionInAnotherManifest = new List<JObject>();

        foreach (var assetToReingest in assetsToReingest)
        {
            // grab the asset(s) from the payload
            var assets = payloadAssets.Where(pa =>
                pa.GetRequiredValue<string>(AssetProperties.Id) == assetToReingest.AssetId!.Asset &&
                pa.GetRequiredValue<int>(AssetProperties.Space) == assetToReingest.AssetId.Space).ToList();

            if (assets.Count == 0) continue;
            
            // if it's tracked in the same manifest, treat it like an untracked asset
            if (assetToReingest.ManifestId != dbManifest?.Id)
            {
                assetsRequiringReingestionInAnotherManifest.AddRange(assets);
            }
            else
            {
                untrackedAssets.AddRange(assets);
            }
        }

        return (untrackedAssets, assetsRequiringReingestionInAnotherManifest);
    }

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

    private async Task<EntityResult?> CreateBatches(int customerId, string manifestId, List<JObject> assets, 
        bool addManifestId, CancellationToken cancellationToken)
    {
        if (assets.Count == 0) return null;

        if (addManifestId)
        {
            foreach (var asset in assets)
                asset[AssetProperties.Manifests] = new JArray(new List<string> { manifestId });
        }
        else
        {
            foreach (var asset in assets.Where(asset => asset.ContainsKey(AssetProperties.Manifests)))
            {
                asset[AssetProperties.Manifests] = null;
            }
        }

        try
        {
            var batches = await dlcsApiClient.IngestAssets(customerId,
                assets,
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

    private class AssetsWithAdditionalWork (List<JObject> untrackedAssets, List<AssetId> dlcsAssetIds, 
        List<JObject> reingestedAssetsInAnotherManifest)
    {
        public List<JObject> UntrackedAssets { get; set; } = untrackedAssets;
        
        public List<AssetId> DlcsAssetIds { get; set; } = dlcsAssetIds;
        
        public List<JObject> ReingestedAssetsInAnotherManifest { get; set; }  = reingestedAssetsInAnotherManifest;
    }
    
    private class AssetIdsWithReingestion(AssetId assetId, bool reingest)
    {
        public AssetId AssetId { get; set; } = assetId;
        
        public bool Reingest { get; set; } = reingest;
    }
}

using System.Diagnostics;
using System.Net;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using Core;
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
                    return DlcsInteractionResult.Fail(ErrorHelper.ErrorCreatingSpace<PresentationManifest>());
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
        var dlcsInteractionRequests = await FindAssetsThatRequireAdditionalWork(request.PresentationManifest, dbManifest, spaceId, spaceCreated, request.CustomerId,
            cancellationToken);

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

    private async Task<List<DlcsInteractionRequest>> FindAssetsThatRequireAdditionalWork(PresentationManifest presentationManifest,
        Models.Database.Collections.Manifest? dbManifest, int? spaceId, bool spaceCreated, int customerId,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Checking for known assets");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        List<DlcsInteractionRequest> dlcsInteractionRequests = [];
        List<(AssetId assetId, PaintedResource paintedResource)> assetsNotFoundInSameManifest = [];

        foreach (var paintedResource in presentationManifest.PaintedResources?.Where(pr => pr.Asset != null) ?? [])
        {
            var assetId = paintedResource.Asset!.GetAssetId(customerId);
            var asset = paintedResource.Asset!;

            if (spaceCreated && assetId.Space == spaceId)
            {

                logger.LogTrace("Asset {AssetId} added to newly created space, so treated as unmanaged", assetId);
                dlcsInteractionRequests.Add(new DlcsInteractionRequest(asset, IngestType.ManifestId, false, assetId));
                continue;
            }

            // managed in this manifest
            if (dbManifest != null)
            {

                logger.LogTrace("Reingested asset {AssetId} found within existing manifest", assetId);
                if (dbManifest.CanvasPaintings?.Any(cp => cp.AssetId == assetId) ?? false)
                {
                    // set the asset to reingest, otherwise ignore the asset
                    if (paintedResource.Reingest)
                    {
                        dlcsInteractionRequests.Add(new DlcsInteractionRequest(asset, IngestType.NoManifestId,
                            false,
                            assetId));
                    }
                    
                    continue;
                }
            }

            assetsNotFoundInSameManifest.Add((assetId, paintedResource));
        }

        List<CanvasPainting> inAnotherManifest = [];

        // managed in another manifest
        foreach (var chunkedAssetsToCheck in assetsNotFoundInSameManifest.Chunk(500))
        {
            var assetIds = chunkedAssetsToCheck.Select(a => a.assetId);
            
            inAnotherManifest.AddRange(dbContext.CanvasPaintings.Where(cp =>
                assetIds.Contains(cp.AssetId) && cp.CustomerId == customerId));
        }
        
        List<(AssetId assetId, PaintedResource paintedResource)> checkDlcs = [];

        foreach (var assetNotFoundInSameManifest in assetsNotFoundInSameManifest)
        {
            if (inAnotherManifest.Any(cp => cp.AssetId == assetNotFoundInSameManifest.assetId))
            {
                if (assetNotFoundInSameManifest.paintedResource.Reingest)
                {
                    logger.LogTrace("Reingested asset {AssetId} found within another manifest", assetNotFoundInSameManifest.assetId);
                    dlcsInteractionRequests.Add(new DlcsInteractionRequest(assetNotFoundInSameManifest.paintedResource.Asset!,
                        IngestType.NoManifestId, true, assetNotFoundInSameManifest.assetId));
                }
                else
                {
                    logger.LogTrace("Asset {AssetId} found within another manifest", assetNotFoundInSameManifest.assetId);
                    dlcsInteractionRequests.Add(new DlcsInteractionRequest(assetNotFoundInSameManifest.paintedResource.Asset!, IngestType.NoIngest,
                        true, assetNotFoundInSameManifest.assetId));
                }

                continue;
            }

            // check in the DLCS (unless reingest where we treat it as an unmanaged asset)
            if (assetNotFoundInSameManifest.paintedResource.Reingest)
            {
                dlcsInteractionRequests.Add(new DlcsInteractionRequest(assetNotFoundInSameManifest.paintedResource.Asset!,
                    IngestType.ManifestId, false, assetNotFoundInSameManifest.assetId));
            }
            else
            {
                checkDlcs.Add(assetNotFoundInSameManifest);
            }
        }

        dlcsInteractionRequests.AddRange(await CheckDlcsForAssets(checkDlcs, customerId, cancellationToken));
        
        stopwatch.Stop();
        
        logger.LogTrace("Checking for known assets took {Elapsed} milliseconds", stopwatch.Elapsed.Milliseconds);

        return dlcsInteractionRequests;
    }

    private async Task<List<DlcsInteractionRequest>> CheckDlcsForAssets(
        List<(AssetId assetId, PaintedResource paintedResource)> assetsToCheck, int customerId, CancellationToken cancellationToken)
    {
        IList<JObject> dlcsAssets = [];
        
        try
        {
            dlcsAssets = await dlcsApiClient.GetCustomerImages(customerId,
                assetsToCheck.Select(a => a.assetId.ToString()).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve DLCS assets");
        }
        
        var dlcsAssetIds = dlcsAssets.Select(d => d.GetAssetId(customerId)).ToList();

        List<DlcsInteractionRequest> interactionRequest = [];
        
        foreach (var assetToCheck in assetsToCheck)
        {
            if (dlcsAssetIds.Contains(assetToCheck.assetId))
            {
                logger.LogTrace("Asset {AssetId} found within the DLCS", assetToCheck.assetId);
                interactionRequest.Add(new DlcsInteractionRequest(assetToCheck.paintedResource.Asset!, IngestType.NoIngest, true,
                    assetToCheck.assetId));
            }
            else
            {
                logger.LogTrace("Asset {AssetId} is unmanaged", assetToCheck.assetId);
                interactionRequest.Add(new DlcsInteractionRequest(assetToCheck.paintedResource.Asset!, IngestType.ManifestId, false,
                    assetToCheck.assetId));
            }
        }
        
        return interactionRequest;
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

    private async Task<EntityResult?> CreateBatches(int customerId, string manifestId, 
        List<DlcsInteractionRequest> dlcsInteractionRequests, CancellationToken cancellationToken)
    {
        if (dlcsInteractionRequests.Count == 0) return null;
        
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
        }
        
        try
        {
            var batches = await dlcsApiClient.IngestAssets(customerId,
                dlcsInteractionRequests.Select(d => d.Asset).ToList(),
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
    
    private class DlcsInteractionRequest (JObject asset, IngestType ingest, 
        bool patch, AssetId assetId)
    {
        public JObject Asset { get; set; } = asset;

        public IngestType Ingest { get; set; } = ingest;
        
        public bool Patch { get; set; } = patch;

        public AssetId AssetId { get; set; } = assetId;
    }

    private enum IngestType
    {
        NoIngest,
        ManifestId,
        NoManifestId
    }
}

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
    
    public bool CanBeBuiltUpfront = canBeBuiltUpfront;
    
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

        var checkedAssets =
            await FindAssetsThatRequireAdditionalWork(assets, dbManifest, request.CustomerId, cancellationToken);
        
        await UpdateDlcsAssets(request, manifestId, cancellationToken, checkedAssets.dlcsAssetIds);

        await RemoveUnusedAssets(dbManifest, assets, manifestId, cancellationToken);

        var batchError = await CreateBatches(request.CustomerId, manifestId, checkedAssets.untrackedAssets,
            cancellationToken);
        
        var canBeBuiltUpfront = checkedAssets.untrackedAssets.Count == 0 && assets.Count > 0;
        return new DlcsInteractionResult(batchError, spaceId, canBeBuiltUpfront);
    }

    private async Task UpdateDlcsAssets(WriteManifestRequest request, string manifestId,
        CancellationToken cancellationToken, List<AssetId> dlcsAssets)
    {
        if (dlcsAssets.Count != 0)
        {
            await dlcsApiClient.UpdateAssetManifest(request.CustomerId,
                dlcsAssets.Select(x => x.ToString()).ToList(), OperationType.Add, [manifestId],
                cancellationToken);
        }
    }

    private async Task RemoveUnusedAssets(Models.Database.Collections.Manifest? dbManifest, List<JObject> assets, 
        string manifestId, CancellationToken cancellationToken)
    {
        if (dbManifest == null) return;

        var canvasPaintingsInDatabase = dbManifest.CanvasPaintings;
        var assetIds = assets.Select(a => a.GetAssetId(dbManifest.CustomerId));

        var assetsToRemove = canvasPaintingsInDatabase.Where(cp => assetIds.All(a => a != cp.AssetId));

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
    
    private async Task<(List<JObject> untrackedAssets, List<AssetId> dlcsAssetIds)> FindAssetsThatRequireAdditionalWork(List<JObject> assets, 
        Models.Database.Collections.Manifest? dbManifest, int customerId, CancellationToken cancellationToken = default)
    {
        var assetIds = assets.Select(a => a.GetAssetId(customerId));
        List<CanvasPainting> assetsInDatabase = [];

        if (dbManifest != null)
        {
            assetsInDatabase = dbContext.CanvasPaintings.Where(cp =>
                    cp.AssetId != null && assetIds.Contains(cp.AssetId) && cp.CustomerId == customerId)
                .ToList();

            // all assets are tracked
            if (assetsInDatabase.Count == assets.Count)
            {
                logger.LogTrace("all assets tracked in database for {ManifestId}", dbManifest.Id);
                return ([], []);
            }
        }

        var assetsToCheckDlcs = assetIds.Where(a => assetsInDatabase.All(b => b.AssetId != a)).ToList();

        IList<JObject> dlcsAssets = [];

        try
        {
            dlcsAssets = await dlcsApiClient.GetCustomerImages(customerId,
                assetsToCheckDlcs.Select(a => a.ToString()).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve DLCS assets");
        }

        var dlcsAssetIds = dlcsAssets.Select(a => a.GetAssetId(customerId)).ToList();

        if (assetsInDatabase.Count + dlcsAssets.Count == assets.Count)
        {
            logger.LogTrace("all assets tracked for {ManifestId}", dbManifest?.Id ?? "new manifest");
            return ([], dlcsAssetIds);
        }
        
        var trackedAssets = CombineTrackedAssets(assets, assetsInDatabase, dlcsAssetIds);
        var untrackedAssets = GetUntrackedAssets(assets, trackedAssets);
        
        return (untrackedAssets, dlcsAssetIds);
    }


    private static List<JObject> CombineTrackedAssets(List<JObject> assets, List<CanvasPainting> assetsInDatabase, List<AssetId> dlcsAssets)
    {
        var combinedAssets = dlcsAssets.ToList();
        combinedAssets.AddRange(assetsInDatabase.Select(a => a.AssetId));

        var trackedAssets = assets.Where(a =>
            combinedAssets.Any(b =>
                b.Asset == a.TryGetValue(AssetProperties.Id)?.ToString() &&
                b.Space == a.TryGetValue(AssetProperties.Space)?.Value<int>())).ToList();
        return trackedAssets;
    }

    private static List<JObject> GetAssetJObjectList(WriteManifestRequest request) =>
        request.PresentationManifest.PaintedResources?
            .Select(p => p.Asset)
            .OfType<JObject>()
            .ToList() ?? [];
    
    private static List<JObject> GetUntrackedAssets(List<JObject> assets, List<JObject> trackedAssets) =>
        assets.Where(
                a => trackedAssets.All(b => b.TryGetValue(AssetProperties.Id) != a.TryGetValue(AssetProperties.Id)))
            .ToList();
    
    private async Task<int?> CreateSpace(int customerId, string manifestId,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating new space for customer {Customer}, Manifest {ManifestId}", customerId, manifestId);
        var newSpace =
            await dlcsApiClient.CreateSpace(customerId, ManifestX.GetDefaultSpaceName(manifestId), cancellationToken);
        return newSpace.Id;
    }

    private async Task<EntityResult?> CreateBatches(int customerId, string manifestId, List<JObject> assets, CancellationToken cancellationToken)
    {
        if (assets.Count == 0) return null;
        
        foreach (var asset in assets)
            asset.Add(AssetProperties.Manifests, new JArray(new List<string> { manifestId }));

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
}

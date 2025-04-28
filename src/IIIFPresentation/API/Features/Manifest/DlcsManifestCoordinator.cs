using System.Net;
using API.Features.Storage.Helpers;
using API.Helpers;
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

public class DlcsInteractionResult(EntityResult? error, int? spaceId)
{
    public EntityResult? Error { get; } = error;
    public int? SpaceId { get; } = spaceId;
    
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
            await FindTrackedAssets(assets, dbManifest, manifestId, request.CustomerId, cancellationToken);
        assets = checkedAssets.untrackedAssets;

        var batchError = await CreateBatches(request.CustomerId, manifestId, assets, cancellationToken);
        return new(batchError, spaceId);
    }
    
    private async Task<(List<JObject> untrackedAssets, List<JObject> trackedAssets)> FindTrackedAssets(List<JObject> assets, 
        Models.Database.Collections.Manifest? dbManifest, string manifestId, int customerId, CancellationToken cancellationToken = default)
    {
        var assetIds = assets.Select(a =>
            AssetId.FromString(
                $"{customerId}/{a.GetRequiredValue(AssetProperties.Space)}/{a.GetRequiredValue(AssetProperties.Id)}"));
        
        List<CanvasPainting> assetsInDatabase = [];

        if (dbManifest != null)
        {
            assetsInDatabase = dbManifest.CanvasPaintings
                .Where(cp => cp.AssetId != null && assetIds.Contains(cp.AssetId)).ToList();

            // all assets are tracked
            if (assetsInDatabase.Count == assets.Count) return ([], assets);
        }

        var assetsToCheckDlcs = assetIds.Where(a => assetsInDatabase.All(b => b.AssetId != a)).ToList();
        var dlcsAssets = await dlcsApiClient.GetAssetsById(customerId, assetsToCheckDlcs, cancellationToken);

        if (dlcsAssets.Any())
        {
            await dlcsApiClient.UpdateAssetWithManifest(customerId,
                dlcsAssets.Select(a => new AssetId(customerId, a.Space, a.Id)).ToList(), OperationType.Add,
                [manifestId], cancellationToken);
        }
        
        if (assetsInDatabase.Count + dlcsAssets.Length == assets.Count) return ([], assets);

        var trackedAssets = CombineTrackedAssets(assets, assetsInDatabase, dlcsAssets);
        var untrackedAssets = GetUntrackedAssets(assets, trackedAssets);
        
        return (untrackedAssets, trackedAssets);
    }


    private static List<JObject> CombineTrackedAssets(List<JObject> assets, List<CanvasPainting> assetsInDatabase, Asset[] dlcsAssets)
    {
        var trackedAssets = assets.Where(a =>
            assetsInDatabase.Any(b => b.AssetId.Asset == a.TryGetValue(AssetProperties.Id)?.ToString())).ToList();
        trackedAssets.AddRange(assets.Where(a =>
            dlcsAssets.Any(b =>
                b.Id == a.TryGetValue(AssetProperties.Id)?.ToString() &&
                b.Space.ToString() == a.TryGetValue(AssetProperties.Space)?.ToString())));
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

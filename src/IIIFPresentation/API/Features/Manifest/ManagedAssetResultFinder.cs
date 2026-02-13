using System.Diagnostics;
using API.Features.Manifest.Exceptions;
using API.Infrastructure.Helpers;
using Core.Exceptions;
using Core.Helpers;
using API.Settings;
using DLCS.API;
using Microsoft.Extensions.Options;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Services.Manifests.Helpers;
using Services.Manifests.Model;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Features.Manifest;

public class ManagedAssetResultFinder(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    IOptions<ApiSettings> options,
    ILogger<ManagedAssetResultFinder> logger) : IManagedAssetResultFinder
{
    private readonly ApiSettings settings = options.Value;
    
    /// <summary>
    /// Checks a presentation manifest to find what assets require further processing by the DLCS
    /// </summary>
    public async Task<List<DlcsInteractionRequest>> FindAssetsThatRequireAdditionalWork(PresentationManifest presentationManifest,
         List<AssetId>? existingAssetIds, int? spaceId, bool spaceCreated, int customerId, CancellationToken cancellationToken)
    {
        logger.LogTrace("Checking for known assets");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        List<DlcsInteractionRequest> dlcsInteractionRequests = [];
        List<(AssetId assetId, PaintedResource paintedResource)> assetsNotFoundInSameManifest = [];

        foreach (var paintedResource in presentationManifest.PaintedResources?.Where(pr => pr.Asset != null) ?? [])
        {
            var asset = paintedResource.Asset!;
            AssetId assetId;

            try
            {
                assetId = asset.GetAssetId(customerId);
            }
            catch (AssetIdException assetIdException)
            {
                if (!string.IsNullOrEmpty(paintedResource.CanvasPainting?.CanvasId))
                {
                    assetIdException.Data.Add(ExceptionDataType.CanvasPaintingId, paintedResource.CanvasPainting?.CanvasId);
                }
                throw;
            }

            if (IsAssetNew(spaceId, spaceCreated, assetId))
            {
                // ingest with a manifest id, then don't patch the manifest id
                logger.LogTrace("Asset {AssetId} added to newly created space, so treated as unmanaged", assetId);
                dlcsInteractionRequests.Add(new DlcsInteractionRequest(asset, IngestType.ManifestId, false, assetId));
                continue;
            }

            // check if the asset is managed in this manifest
            if (existingAssetIds != null)
            {
                if (existingAssetIds.Any(cp => cp == assetId))
                {
                    // set the asset to reingest, otherwise ignore the asset
                    if (settings.AlwaysReingest || (paintedResource.Reingest ?? false))
                    {
                        // ingest without the manifest id, then don't patch the manifest id
                        logger.LogTrace("Asset {AssetId} found within existing manifest - reingest", assetId);
                        dlcsInteractionRequests.Add(new DlcsInteractionRequest(asset, IngestType.NoManifestId,
                            false,
                            assetId));
                    }
                    
                    // if it's not reingested, and on the same manifest, ignore
                    continue;
                }
            }

            assetsNotFoundInSameManifest.Add((assetId, paintedResource));
        }

        var inAnotherManifest = FindAssetsInAnotherManifest(customerId, assetsNotFoundInSameManifest);
        
        List<(AssetId assetId, PaintedResource paintedResource)> checkDlcs = [];

        // run through every other asset
        foreach (var assetNotFoundInSameManifest in assetsNotFoundInSameManifest)
        {
            // is this asset found in another manifest?
            if (inAnotherManifest.Any(cp => cp.AssetId == assetNotFoundInSameManifest.assetId))
            {
                IngestType ingestType;
                
                if (settings.AlwaysReingest || (assetNotFoundInSameManifest.paintedResource.Reingest ?? false))
                {
                    // reingest - ingest with no manifest id, then patch afterwards
                    logger.LogTrace("Asset {AssetId} found within another manifest - reingest", assetNotFoundInSameManifest.assetId);
                    ingestType = IngestType.NoManifestId;
                }
                else
                {
                    // not reingest - don't ingest, then patch the manifest id
                    logger.LogTrace("Asset {AssetId} found within another manifest", assetNotFoundInSameManifest.assetId);
                    ingestType = IngestType.NoIngest;
                }
                
                dlcsInteractionRequests.Add(new DlcsInteractionRequest(assetNotFoundInSameManifest.paintedResource.Asset!, ingestType,
                    true, assetNotFoundInSameManifest.assetId));
                continue;
            }

            // check in the DLCS (unless reingest where we treat it as an unmanaged asset)
            if (settings.AlwaysReingest || (assetNotFoundInSameManifest.paintedResource.Reingest ?? false))
            {
                // ingest with the manifest id, then don't patch the manifest id
                logger.LogTrace("Asset {AssetId} not found within another manifest, but set to reingest", assetNotFoundInSameManifest.assetId);
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
    
    /// <summary>
    /// Method looks in the provided manifest to find any assets, provided in the Items property
    /// that are managed by the configured DLCS instance
    /// </summary>
    public async Task<List<AssetId>> CheckAssetsFromItemsExist(
        List<InterimCanvasPainting>? interimCanvasPaintings, int customerId, List<AssetId>? existingAssetIds, CancellationToken cancellationToken)
    {
        if (interimCanvasPaintings.IsNullOrEmpty()) return [];

        List<AssetId> assetsToAddToManifest = [];
        List<AssetId> trackedAssets = [];
        
        var assetIdsFromItems = interimCanvasPaintings.GetAssetIds();

        foreach (var assetId in assetIdsFromItems)
        {
            if (existingAssetIds != null && existingAssetIds.Any(cp => cp == assetId))
            {
                trackedAssets.Add(assetId);
            }
            else
            {
                assetsToAddToManifest.Add(assetId);
            }
        }

        var assetsToCheckInDlcs =
            assetsToAddToManifest.Where(asset =>
            {
                if (dbContext.CanvasPaintings.Any(cp => cp.CustomerId == customerId && cp.AssetId == asset))
                {
                    trackedAssets.Add(asset);
                    return false;
                };
                
                return true;
            });

        IList<JObject> dlcsAssets;
        try
        {
            dlcsAssets = await dlcsApiClient.GetCustomerImages(customerId,
                assetsToCheckInDlcs.Select(a => a.ToString()).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve DLCS assets");
            throw;
        }

        var dlcsAssetIds = dlcsAssets.Select(d => d.GetAssetId(customerId));
        trackedAssets.AddRange(dlcsAssetIds);

        var missingAssets = interimCanvasPaintings.Where(icp => !trackedAssets.Any(a =>
            icp.SuspectedAssetId == a.Asset && icp.CustomerId == a.Customer && icp.SuspectedSpace == a.Space)).ToList();
        
        if (missingAssets.Count != 0)
        {
            throw new PresentationException(
                $"Suspected DLCS assets from items not found: {string.Join(", ", 
                    missingAssets.Select(a => $"(id: {a.CanvasOriginalId}, assetId: {new AssetId(customerId, a.SuspectedSpace!.Value, a.SuspectedAssetId!)})"))}");
        }

        return assetsToAddToManifest;
    }

    private List<CanvasPainting> FindAssetsInAnotherManifest(int customerId, 
        List<(AssetId assetId, PaintedResource paintedResource)> assetsNotFoundInSameManifest)
    {
        List<CanvasPainting> inAnotherManifest = [];
        
        foreach (var chunkedAssetsToCheck in assetsNotFoundInSameManifest.Chunk(500))
        {
            var assetIds = chunkedAssetsToCheck.Select(a => a.assetId);
            
            inAnotherManifest.AddRange(dbContext.CanvasPaintings.Where(cp =>
                assetIds.Contains(cp.AssetId) && cp.CustomerId == customerId));
        }

        return inAnotherManifest;
    }

    /// <summary>
    /// Whether we can determine that the asset is new 
    /// </summary>
    private static bool IsAssetNew(int? spaceId, bool spaceCreated, AssetId assetId)
    {
        // if the space has been created, and the id of the space is the same as the space id, it means that the asset
        // has been updated with the new space and therefore must be a new asset
        return spaceCreated && assetId.Space == spaceId;
    }

    private async Task<List<DlcsInteractionRequest>> CheckDlcsForAssets(
        List<(AssetId assetId, PaintedResource paintedResource)> assetsToCheck, int customerId, CancellationToken cancellationToken)
    {
        IList<JObject> dlcsAssets;
        
        try
        {
            dlcsAssets = await dlcsApiClient.GetCustomerImages(customerId,
                assetsToCheck.Select(a => a.assetId.ToString()).ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve DLCS assets");
            throw;
        }
        
        var dlcsAssetIds = dlcsAssets.Select(d => d.GetAssetId(customerId)).ToList();

        List<DlcsInteractionRequest> interactionRequest = [];
        
        foreach (var assetToCheck in assetsToCheck)
        {
            IngestType ingestType;
            bool patch;
            
            // is the asset managed in the DLCS?
            if (dlcsAssetIds.Contains(assetToCheck.assetId))
            {
                // don't ingest, then patch the manifest id
                logger.LogTrace("Asset {AssetId} found within the DLCS", assetToCheck.assetId);
                ingestType = IngestType.NoIngest;
                patch = true;
            }
            else
            {
                // ingest with the manifest id, then don't patch the manifest id
                logger.LogTrace("Asset {AssetId} is unmanaged", assetToCheck.assetId);
                ingestType = IngestType.ManifestId;
                patch = false;
            }
            
            interactionRequest.Add(new DlcsInteractionRequest(assetToCheck.paintedResource.Asset!, ingestType, patch,
                assetToCheck.assetId));
        }
        
        return interactionRequest;
    }
}

/// <summary>
/// Checks assets for if they exist somewhere else and sets what should happen
/// </summary>
public interface IManagedAssetResultFinder
{
    public Task<List<DlcsInteractionRequest>> FindAssetsThatRequireAdditionalWork(
        PresentationManifest presentationManifest,
        List<AssetId>? existingAssetIds, int? spaceId, bool spaceCreated, int customerId,
        CancellationToken cancellationToken);
    
    public Task<List<AssetId>> CheckAssetsFromItemsExist(
        List<InterimCanvasPainting>? itemCanvasPaintingsWithAssets,
        int customerId, List<AssetId>? existingAssetIds, CancellationToken cancellationToken);
}

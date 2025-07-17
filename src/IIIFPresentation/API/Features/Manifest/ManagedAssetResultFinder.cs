﻿using System.Diagnostics;
using API.Infrastructure.Helpers;
using DLCS.API;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Features.Manifest;

public class ManagedAssetResultFinder(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    ILogger<ManagedAssetResultFinder> logger) : IManagedAssetResultFinder
{
    /// <summary>
    /// Checks a presentation manifest to find what assets require further processing by the DLCS
    /// </summary>
    public async Task<List<DlcsInteractionRequest>> FindAssetsThatRequireAdditionalWork(PresentationManifest presentationManifest,
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
            var asset = paintedResource.Asset!;
            var assetId = asset.GetAssetId(customerId);

            if (IsAssetNew(spaceId, spaceCreated, assetId))
            {
                // ingest with a manifest id, then don't patch the manifest id
                logger.LogTrace("Asset {AssetId} added to newly created space, so treated as unmanaged", assetId);
                dlcsInteractionRequests.Add(new DlcsInteractionRequest(asset, IngestType.ManifestId, false, assetId));
                continue;
            }

            // check if the asset is managed in this manifest
            if (dbManifest != null)
            {
                if (dbManifest.CanvasPaintings?.Any(cp => cp.AssetId == assetId) ?? false)
                {
                    // set the asset to reingest, otherwise ignore the asset
                    if (paintedResource.Reingest)
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
            // is this this asset is found in another manifest?
            if (inAnotherManifest.Any(cp => cp.AssetId == assetNotFoundInSameManifest.assetId))
            {
                IngestType ingestType;
                
                if (assetNotFoundInSameManifest.paintedResource.Reingest)
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
            if (assetNotFoundInSameManifest.paintedResource.Reingest)
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
        IList<JObject> dlcsAssets = [];
        
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
        Models.Database.Collections.Manifest? dbManifest, int? spaceId, bool spaceCreated, int customerId,
        CancellationToken cancellationToken);
}

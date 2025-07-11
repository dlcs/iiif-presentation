using System.Diagnostics;
using API.Infrastructure.Helpers;
using DLCS.API;
using Models.API.Manifest;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using CanvasPainting = Models.Database.CanvasPainting;

namespace API.Features.Manifest;

public class KnownAssetChecker(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    ILogger<KnownAssetChecker> logger) : IKnownAssetChecker
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
}

/// <summary>
/// Checks assets for if they exist somewhere else
/// </summary>
public interface IKnownAssetChecker
{
    public Task<List<DlcsInteractionRequest>> FindAssetsThatRequireAdditionalWork(
        PresentationManifest presentationManifest,
        Models.Database.Collections.Manifest? dbManifest, int? spaceId, bool spaceCreated, int customerId,
        CancellationToken cancellationToken);
}

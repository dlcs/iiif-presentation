using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using Core;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.Collections;
using Newtonsoft.Json.Linq;
using Repository;

namespace API.Features.Manifest;

public class DlcsManifestCoordinator(
    IDlcsApiClient dlcsApiClient,
    PresentationContext dbContext,
    ILogger<DlcsManifestCoordinator> logger)
{
    /// <summary>
    /// Carry out any required interactions with DLCS for given <see cref="WriteManifestRequest"/>, this can include
    /// creating a space and/or creating DLCS batches
    /// </summary>
    /// <returns>Tuple of any errors encountered and new Manifest SpaceId</returns>
    public async Task<(ModifyEntityResult<PresentationManifest, ModifyCollectionType>? error, int? space)> HandleDlcsInteractions(WriteManifestRequest request,
        string manifestId, CancellationToken cancellationToken)
    {
        // NOTE - this must always happen before handing off to canvasPaintingResolve
        var assets = GetAssetJObjectList(request);

        if (!request.CreateSpace && assets.Count <= 0)
        {
            logger.LogDebug("No assets or space required, DLCS integrations not required");
            return (null, null);
        }

        if (assets.Any(a => !a.HasValues)) return (ErrorHelper.CouldNotRetrieveAssetId<PresentationManifest>(), null);

        int? spaceId = null;
        var assetsWithoutSpaces = assets.Where(a => !a.TryGetValue(AssetProperties.Space, out _)).ToArray();
        if (request.CreateSpace || assetsWithoutSpaces.Length > 0)
        {
            // Either you want a space or we detected you need a space regardless
            spaceId = await CreateSpace(request.CustomerId, manifestId, cancellationToken);
            if (!spaceId.HasValue) return (ErrorHelper.ErrorCreatingSpace<PresentationManifest>(), null);

            foreach (var asset in assetsWithoutSpaces)
                asset.Add(AssetProperties.Space, spaceId.Value);
        }

        var batchError = await CreateBatches(request.CustomerId, manifestId, assets, cancellationToken);
        return (batchError, spaceId);
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

    private async Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>?> CreateBatches(int customerId, string manifestId, List<JObject> assets, CancellationToken cancellationToken)
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
            return ModifyEntityResult<PresentationManifest, ModifyCollectionType>.Failure(exception.Message, ModifyCollectionType.DlcsException,
                WriteResult.Error);
        }
    }
}

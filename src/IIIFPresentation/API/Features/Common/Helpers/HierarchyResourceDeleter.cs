using API.Features.Storage.Helpers;
using API.Features.Manifest;
using API.Infrastructure.Helpers;
using AWS.Helpers;
using Core;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Models.Database.Collections;
using Models.DLCS;
using Repository;
using Services.TextServices;

namespace API.Features.Common.Helpers;

public class HierarchyResourceDeleter(
    PresentationContext dbContext,
    IIIIFS3Service iiifS3,
    IPipelineJobService pipelineJobService,
    DlcsManifestCoordinator dlcsManifestCoordinator,
    ILogger<HierarchyResourceDeleter> logger)
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> DeleteResource<T>(string? etagFromRequest, int customerId, 
        string resourceId, CancellationToken cancellationToken) where T : class, IHierarchyResource
    {
        var resource = await dbContext.Set<T>().Retrieve(resourceId, true, cancellationToken);
        
        if (resource is null) return DeleteErrorHelper.NotFound();

        if (!EtagComparer.IsMatch(resource.Etag, etagFromRequest)) return DeleteErrorHelper.EtagNotMatching();

        List<AssetId> manifestAssetIds = [];

        switch (resource)
        {
            case Collection collection:
            {
                var error = await DeleteCollection(resource, collection, cancellationToken);
                if (error != null) return error;
                break;
            }
            case Models.Database.Collections.Manifest manifest:
                manifestAssetIds = await GetManifestAssetIds(manifest, cancellationToken);
                await DeleteManifest(resource, manifest, cancellationToken);
                break;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var hierarchyType = typeof(T);
            var resourceType = hierarchyType == typeof(Collection) ? "collection" : "manifest";
            
            logger.LogError(ex, "Error attempting to delete {ResourceType} {ResourceId} for customer {CustomerId}",
                resourceType, resourceId, customerId);
            return DeleteErrorHelper.UnknownError(resourceType);
        }

        // Assets are only detached once the delete is committed, so a failed delete leaves them untouched
        await RemoveManifestFromAssets(customerId, resourceId, manifestAssetIds, cancellationToken);

        return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.Deleted);
    }
    
    private async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>?> DeleteCollection(IHierarchyResource resource, 
        Collection collection, CancellationToken cancellationToken)
    {
        var hasItems = await dbContext.Hierarchy.AnyAsync(c => c.Parent == collection.Id,
            cancellationToken: cancellationToken);

        if (hasItems)
        {
            return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.BadRequest,
                DeleteResourceErrorType.CollectionNotEmpty, "Cannot delete a collection with child items");
        }
        
        dbContext.Remove(collection);

        if (!collection.IsStorageCollection)
        {
            await iiifS3.DeleteIIIFFromS3(resource);
        }

        return null;
    }
    
    private async Task DeleteManifest(IHierarchyResource resource, Models.Database.Collections.Manifest manifest,
        CancellationToken cancellationToken)
    {
        dbContext.Remove(manifest);
        await Task.WhenAll(
            iiifS3.DeleteIIIFFromS3(resource),
            DeletePipelineJobSafely(manifest, cancellationToken));
    }

    private Task<List<AssetId>> GetManifestAssetIds(Models.Database.Collections.Manifest manifest,
        CancellationToken cancellationToken) =>
        dbContext.CanvasPaintings
            .Where(cp => cp.CustomerId == manifest.CustomerId && cp.ManifestId == manifest.Id && cp.AssetId != null)
            .Select(cp => cp.AssetId!)
            .ToListAsync(cancellationToken);

    private async Task RemoveManifestFromAssets(int customerId, string manifestId, List<AssetId> assetIds,
        CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0) return;

        try
        {
            await dlcsManifestCoordinator.RemoveManifestsFromAssets(manifestId, customerId, assetIds,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // The manifest is already deleted at this point, so a DLCS failure is logged rather than failing the request
            logger.LogError(ex, "Error removing manifest {ManifestId} from assets for customer {CustomerId}",
                manifestId, customerId);
        }
    }

    private async Task DeletePipelineJobSafely(Models.Database.Collections.Manifest manifest,
        CancellationToken cancellationToken)
    {
        try
        {
            await pipelineJobService.DeletePipelineJob(manifest, cancellationToken);
        }
        catch (Exception ex)
        {
            // Text-services being unreachable shouldn't stop the manifest itself from being deleted
            logger.LogWarning(ex, "Error deleting text-services job for manifest {ManifestId}", manifest.Id);
        }
    }

}

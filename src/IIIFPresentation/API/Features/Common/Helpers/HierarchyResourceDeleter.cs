using API.Infrastructure.Helpers;
using AWS.Helpers;
using Core;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;

namespace API.Features.Common.Helpers;

public class HierarchyResourceDeleter(
    PresentationContext dbContext,
    IIIIFS3Service iiifS3,
    ILogger<HierarchyResourceDeleter> logger)
{
    public async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>> DeleteResource(string? etagFromRequest, 
        IHierarchyResource? resource, CancellationToken cancellationToken)
    {
        if (resource is null) return DeleteErrorHelper.NotFound();

        if (!EtagComparer.IsMatch(resource.Etag, etagFromRequest)) return DeleteErrorHelper.EtagNotMatching();
        
        switch (resource)
        {
            case Collection collection:
            {
                var error = await DeleteCollection(resource, collection, cancellationToken);
                if (error != null) return error;
                break;
            }
            case Models.Database.Collections.Manifest manifest:
                await DeleteManifest(resource, manifest);
                break;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var hierarchy = resource.Hierarchy.GetCanonical();
            var resourceType = hierarchy.CollectionId != null ? "collection" : "manifest";
            
            logger.LogError(ex, "Error attempting to delete {ResourceType} {ResourceId} for customer {CustomerId}",
                resourceType, hierarchy.CollectionId ?? hierarchy.ManifestId, hierarchy.CustomerId);
            return DeleteErrorHelper.UnknownError(resourceType);
        }

        return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.Deleted);
    }
    
    private async Task<ResultMessage<DeleteResult, DeleteResourceErrorType>?> DeleteCollection(IHierarchyResource resource, 
        Collection collection, CancellationToken cancellationToken)
    {
        var hasItems = await dbContext.Hierarchy.AnyAsync(
            c => c.CustomerId == collection.CustomerId && c.Parent == collection.Id,
            cancellationToken: cancellationToken);

        if (hasItems)
        {
            return new ResultMessage<DeleteResult, DeleteResourceErrorType>(DeleteResult.BadRequest,
                DeleteResourceErrorType.CollectionNotEmpty, "Cannot delete a collection with child items");
        }
        
        dbContext.Collections.Remove(collection);

        if (!collection.IsStorageCollection)
        {
            await iiifS3.DeleteIIIFFromS3(resource);
        }

        return null;
    }
    
    private async Task DeleteManifest(IHierarchyResource resource, Models.Database.Collections.Manifest manifest)
    {
        dbContext.Manifests.Remove(manifest);
        await iiifS3.DeleteIIIFFromS3(resource);
    }

}

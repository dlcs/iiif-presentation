using API.Infrastructure.Requests;
using Core;
using Microsoft.EntityFrameworkCore;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Helpers;

public static class PresentationContextX
{
    public static async Task<ModifyEntityResult<T, ModifyCollectionType>?> TrySaveCollection<T>(
        this PresentationContext dbContext, 
        int customerId, 
        ILogger logger,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        { 
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex,"Error creating collection for customer {Customer} in the database", customerId);

            if (ex.IsCustomerIdSlugParentViolation())
            {
                return ModifyEntityResult<T, ModifyCollectionType>.Failure(
                    $"The collection could not be created due to a duplicate slug value",
                    ModifyCollectionType.DuplicateSlugValue, WriteResult.Conflict);
            }

            return ModifyEntityResult<T, ModifyCollectionType>.Failure(
                $"The collection could not be created", ModifyCollectionType.DuplicateSlugValue, WriteResult.Conflict);
        }

        return null;
    }
    
    public static async Task<Collection?> RetrieveCollection(this PresentationContext dbContext,  int customerId, 
        string collectionId, CancellationToken cancellationToken)
    {
        var collection = await dbContext.Collections.AsNoTracking().FirstOrDefaultAsync(
            s => s.CustomerId == customerId && s.Id == collectionId,
            cancellationToken);
        
        return collection;
    }
    
    public static async Task<Hierarchy> RetrieveHierarchyAsync(this PresentationContext dbContext,  int customerId, 
        string resourceId, ResourceType resourceType, CancellationToken cancellationToken = default)
    {
        var hierarchy = await dbContext.Hierarchy.AsNoTracking().FirstAsync(
            s => s.CustomerId == customerId && s.ResourceId == resourceId && s.Type == resourceType,
            cancellationToken);
        
        return hierarchy;
    }
    
    public static IQueryable<Collection> RetrieveHierarchicalItems(this PresentationContext dbContext, int customerId, string resourceId)
    {
        
        var hierarchicalItems = dbContext.Hierarchy.AsNoTracking()
            .Where(h => h.CustomerId == customerId && h.Parent == resourceId).Select(x => x.ResourceId);
        return dbContext.Collections
            .Where(s => s.CustomerId == customerId &&
                        hierarchicalItems.Contains(s.Id));
    }
}
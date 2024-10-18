using API.Infrastructure.Requests;
using Core;
using Microsoft.EntityFrameworkCore;
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
                $"The collection could not be created", ModifyCollectionType.Unknown);
        }

        return null;
    }

    /// <summary>
    /// Retrieves a collection from the database, with the Hierarchy records included
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="collectionId">The collection to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static Task<Collection?> RetrieveCollectionAsync(this PresentationContext dbContext, int customerId,
        string collectionId, bool tracked = false, CancellationToken cancellationToken = default)
        => dbContext.Collections.Retrieve(customerId, collectionId, tracked, cancellationToken);

    /// <summary>
    /// Retrieves a <see cref="IHierarchyResource"/> from database, with Hierarchy records included
    /// </summary>
    /// <param name="entities">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="resourceId">The collection/manifest Id to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">Current cancellation token</param>
    /// <returns>The retrieved <see cref="IHierarchyResource"/></returns>
    public static async Task<T?> Retrieve<T>(this DbSet<T> entities,
        int customerId, string resourceId, bool tracked = false, CancellationToken cancellationToken = default)
        where T : class, IHierarchyResource
    {
        var resources = tracked ? entities : entities.AsNoTracking();

        return await resources
            .Include(e => e.Hierarchy)
            .FirstOrDefaultAsync(e => e.CustomerId == customerId && e.Id == resourceId, cancellationToken);
    }

    /// <summary>
    /// Retrieves child collections from the database of the parent record, while including the hierarchy records
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="collectionId">The collection to retrieve child items for</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <returns>A query containing child collections</returns>
    public static IQueryable<Collection> RetrieveCollectionItems(this PresentationContext dbContext, int customerId, 
        string collectionId, bool tracked = false)
    {
        var collection = tracked ? dbContext.Collections : dbContext.Collections.AsNoTracking();
        return collection.Include(c => c.Hierarchy)
            .Where(c => c.CustomerId == customerId && c.Hierarchy!.Single(h => h.Canonical).Parent == collectionId);
    }
    
    public static async Task<int> GetTotalItemCountForCollection(this PresentationContext dbContext, Collection collection, 
        int itemCount, int pageSize, CancellationToken cancellationToken = default)
    {
        int total;
        if (itemCount < pageSize)
        {
            // there can't be more as we've asked for PageSize and got less 
            total = itemCount;
        }
        else
        {
            // if we get PageSize back then there may be more in db
            total = await dbContext.Hierarchy.CountAsync(
                c => c.CustomerId == collection.CustomerId && c.Parent == collection.Id,
                cancellationToken: cancellationToken);
        }

        return total;
    }
}
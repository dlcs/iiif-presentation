﻿using API.Infrastructure.Requests;
using Core;
using IIIF;
using Microsoft.EntityFrameworkCore;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using DbManifest = Models.Database.Collections.Manifest;

namespace API.Features.Storage.Helpers;

public static class PresentationContextX
{
    public static Task<ModifyEntityResult<T, ModifyCollectionType>?> TrySaveCollection<T>(
        this PresentationContext dbContext,
        int customerId,
        ILogger logger,
        CancellationToken cancellationToken)
        where T : JsonLdBase
        => dbContext.TrySave<T>("collection", customerId, logger, cancellationToken);
    
    public static async Task<ModifyEntityResult<T, ModifyCollectionType>?> TrySave<T>(
        this PresentationContext dbContext, 
        string resourceType,
        int customerId, 
        ILogger logger,
        CancellationToken cancellationToken)
        where T : JsonLdBase
    {
        try
        { 
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "DB Error saving {ResourceType} for customer {Customer}", resourceType, customerId);

            if (ex.IsCustomerIdSlugParentViolation())
            {
                return ModifyEntityResult<T, ModifyCollectionType>.Failure(
                    $"The {resourceType} could not be created due to a duplicate slug value",
                    ModifyCollectionType.DuplicateSlugValue, WriteResult.Conflict);
            }

            return ModifyEntityResult<T, ModifyCollectionType>.Failure(
                $"The {resourceType} could not be created", ModifyCollectionType.Unknown);
        }

        return null;
    }

    /// <summary>
    /// Retrieves a manifest from the database, with the Hierarchy records included
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="manifestId">The manifest to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="withCanvasPaintings">Whether the CanvasPaintings records should be included</param>
    /// <param name="withBatches">Whether the Batches records should be included</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static Task<DbManifest?> RetrieveManifestAsync(this PresentationContext dbContext, int customerId,
        string manifestId, bool tracked = false, bool withCanvasPaintings = true, bool withBatches = false, CancellationToken cancellationToken = default)
    {
        IQueryable<DbManifest> dbContextManifests = dbContext.Manifests;

        if (withCanvasPaintings)
        {
            dbContextManifests = dbContextManifests.Include(m => m.CanvasPaintings).AsSplitQuery();
        }

        if (withBatches)
        {
            dbContextManifests = dbContextManifests.Include(m => m.Batches);
        }
        
        return dbContextManifests.Retrieve(customerId, manifestId, tracked, cancellationToken);
    }
    
    /// <summary>
    /// Retrieves a 'full' collection from the database, with the Hierarchy records (including Parent)
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="collectionId">The collection to retrieve</param>
    /// <param name="tracked">Whether the resource should be tracked or not</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>The retrieved collection</returns>
    public static Task<Collection?> RetrieveCollectionWithParentAsync(this PresentationContext dbContext, int customerId,
        string collectionId, bool tracked = false, CancellationToken cancellationToken = default)
    {
        var collections = tracked ? dbContext.Collections : dbContext.Collections.AsNoTracking();
        return collections
            .Include(e => e.Hierarchy)!.ThenInclude(h => h.ParentCollection)
            .FirstOrDefaultAsync(e => e.CustomerId == customerId && e.Id == collectionId, cancellationToken);
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
    public static async Task<T?> Retrieve<T>(this IQueryable<T> entities,
        int customerId, string resourceId, bool tracked = false, CancellationToken cancellationToken = default)
        where T : class, IHierarchyResource
    {
        var resources = tracked ? entities : entities.AsNoTracking();

        return await resources
            .Include(e => e.Hierarchy)
            .FirstOrDefaultAsync(e => e.CustomerId == customerId && e.Id == resourceId, cancellationToken);
    }
    
    /// <summary>
    /// Retrieves child hierarchy items for the parent record - entities are not tracked
    /// </summary>
    /// <param name="dbContext">The context to pull records from</param>
    /// <param name="customerId">Customer the record is attached to</param>
    /// <param name="resourceId">The collection to retrieve child items for</param>
    /// <param name="publicOnly">Whether to return public only resources</param>
    /// <returns>A query containing child collections</returns>
    public static IQueryable<Hierarchy> RetrieveCollectionItems(this PresentationContext dbContext, int customerId, 
        string resourceId, bool publicOnly = false)
    {
        var hierarchy = dbContext.Hierarchy.AsNoTracking()
            .Include(h => h.Collection)
            .Include(h => h.Manifest)
            .Where(c => c.CustomerId == customerId && c.Canonical && c.Parent == resourceId);

        if (publicOnly)
        {
            hierarchy = hierarchy.Where(c => (c.Collection != null && c.Collection.IsPublic) ||  
                                             (c.Manifest != null && c.Manifest.LastProcessed != null));
        }

        return hierarchy;
    }

    public static async Task<int> GetTotalItemCountForCollection(this PresentationContext dbContext,
        Collection collection, int itemCount, int pageSize, int pageNo, CancellationToken cancellationToken = default)
    {
        int total;
        if (itemCount > 0 && itemCount < pageSize)
        {
            // there can't be more as we've asked for PageSize and got less 
            total = itemCount + (pageNo - 1) * pageSize;
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

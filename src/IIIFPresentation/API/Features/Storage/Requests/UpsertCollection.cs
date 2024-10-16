using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using Core;
using Core.Exceptions;
using Core.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class UpsertCollection(int customerId, string collectionId, UpsertFlatCollection collection, UrlRoots urlRoots, 
    string? eTag)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; set; } = collectionId;

    public UpsertFlatCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
    
    public string? ETag { get; set; } = eTag;
}

public class UpsertCollectionHandler(
    PresentationContext dbContext,
    IETagManager eTagManager,
    ILogger<UpsertCollectionHandler> logger,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpsertCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(UpsertCollection request, 
        CancellationToken cancellationToken)
    {
        var databaseCollection =
            await dbContext.Collections.FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken);
        
        Hierarchy hierarchy;

        if (databaseCollection == null)
        {
            if (request.ETag is not null)
            {
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "ETag should not be added when inserting a collection via PUT", ModifyCollectionType.ETagNotAllowed,
                    WriteResult.PreConditionFailed);
            }

            var createdDate = DateTime.UtcNow;
            
            var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
                request.Collection.Parent.GetLastPathElement(), cancellationToken);
            
            if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

            databaseCollection = new Collection
            {
                Id = request.CollectionId,
                Created = createdDate,
                Modified = createdDate,
                CreatedBy = Authorizer.GetUser(),
                CustomerId = request.CustomerId,
                IsPublic = request.Collection.Behavior.IsPublic(),
                IsStorageCollection = request.Collection.Behavior.IsStorageCollection(),
                Label = request.Collection.Label,
                Thumbnail = request.Collection.PresentationThumbnail,
                Tags = request.Collection.Tags,
            };
            
            hierarchy = new Hierarchy
            {
                CollectionId = request.CollectionId,
                Type = ResourceType.IIIFCollection,
                Slug = request.Collection.Slug,
                CustomerId = request.CustomerId,
                Canonical = true,
                ItemsOrder = request.Collection.ItemsOrder,
                Parent = parentCollection.Id,
                Public = request.Collection.Behavior.IsPublic(),
            };
            
            await dbContext.AddAsync(databaseCollection, cancellationToken);
            await dbContext.AddAsync(hierarchy, cancellationToken);
        }
        else
        {
            eTagManager.TryGetETag($"/{request.CustomerId}/collections/{request.CollectionId}", out var eTag);

            if (request.ETag != eTag)
            {
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "ETag does not match", ModifyCollectionType.ETagNotMatched, WriteResult.PreConditionFailed);
            }

            hierarchy = await dbContext.RetrieveHierarchyAsync(request.CustomerId, request.CollectionId,
                databaseCollection.IsStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection,
                cancellationToken);

            if (hierarchy.Parent != request.Collection.Parent)
            {
                var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
                    request.Collection.Parent.GetLastPathElement(), cancellationToken);

                if (parentCollection == null)
                {
                    return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                        $"The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound,
                        WriteResult.BadRequest);
                }
            }

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            databaseCollection.IsPublic = request.Collection.Behavior.IsPublic();
            databaseCollection.IsStorageCollection = request.Collection.Behavior.IsStorageCollection();
            databaseCollection.Label = request.Collection.Label;
            databaseCollection.Thumbnail = request.Collection.PresentationThumbnail;
            databaseCollection.Tags = request.Collection.Tags;

            hierarchy.Parent = request.Collection.Parent;
            hierarchy.ItemsOrder = request.Collection.ItemsOrder;
            hierarchy.Public = request.Collection.Behavior.IsPublic();
            hierarchy.Slug = request.Collection.Slug;
        }

        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
            
        var total = await dbContext.Hierarchy.CountAsync(
            c => c.CustomerId == request.CustomerId && c.Parent == databaseCollection.Id,
            cancellationToken: cancellationToken);
        var items = dbContext.RetrieveCollectionItems(request.CustomerId, databaseCollection.Id)
            .Take(settings.PageSize);

        foreach (var item in items)
        { 
            item.FullPath = hierarchy.GenerateFullPath(item.Hierarchy!.Single(h => h.Canonical).Slug);
        }

        if (hierarchy.Parent != null)
        {
            try
            {
                databaseCollection.FullPath =
                    CollectionRetrieval.RetrieveFullPathForCollection(databaseCollection, dbContext);
            }
            catch (PresentationException)
            {
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "New slug exceeds 1000 records.  This could mean an item no longer belongs to the root collection.",
                     ModifyCollectionType.PossibleCircularReference, WriteResult.BadRequest);
            }
        }
        
        await transaction.CommitAsync(cancellationToken);

        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            databaseCollection.ToFlatCollection(request.UrlRoots, settings.PageSize, DefaultCurrentPage, total,
                await items.ToListAsync(cancellationToken: cancellationToken)));
    }
}
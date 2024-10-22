using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using Core;
using Core.Exceptions;
using Core.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class UpsertCollection(int customerId, string collectionId, PresentationCollection collection, UrlRoots urlRoots, 
    string? eTag, string rawRequestBody)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; set; } = collectionId;

    public PresentationCollection Collection { get; } = collection;

    public UrlRoots UrlRoots { get; } = urlRoots;
    
    public string? ETag { get; set; } = eTag;
    
    public string RawRequestBody { get; set; } = rawRequestBody;
}

public class UpsertCollectionHandler(
    PresentationContext dbContext,
    IETagManager eTagManager,
    ILogger<UpsertCollectionHandler> logger,
    IBucketWriter bucketWriter,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpsertCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(UpsertCollection request, 
        CancellationToken cancellationToken)
    {
        var databaseCollection =
            await dbContext.RetrieveCollectionAsync(request.CustomerId, request.CollectionId, true, cancellationToken);

        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        
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
            
            var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                request.Collection.Parent.GetLastPathElement(), true, cancellationToken);
            
            if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

            databaseCollection = new Collection
            {
                Id = request.CollectionId,
                Created = createdDate,
                Modified = createdDate,
                CreatedBy = Authorizer.GetUser(),
                CustomerId = request.CustomerId,
                IsPublic = request.Collection.Behavior.IsPublic(),
                IsStorageCollection = isStorageCollection,
                Label = request.Collection.Label,
                Tags = request.Collection.Tags,
            };
            
            hierarchy = new Hierarchy
            {
                CollectionId = request.CollectionId,
                Type = isStorageCollection
                    ? ResourceType.StorageCollection
                    : ResourceType.IIIFCollection,
                Slug = request.Collection.Slug,
                CustomerId = request.CustomerId,
                Canonical = true,
                ItemsOrder = request.Collection.ItemsOrder,
                Parent = parentCollection.Id
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
            
            if (isStorageCollection && !databaseCollection.IsStorageCollection)
            {
                return ErrorHelper.CannotChangeToStorageCollection<PresentationCollection>();
            }

            hierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

            if (hierarchy.Parent != request.Collection.Parent)
            {
                var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                    request.Collection.Parent.GetLastPathElement(), cancellationToken: cancellationToken);

                if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();
            }

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            databaseCollection.IsPublic = request.Collection.Behavior.IsPublic();
            databaseCollection.IsStorageCollection = isStorageCollection;
            databaseCollection.Label = request.Collection.Label;
            databaseCollection.Tags = request.Collection.Tags;

            hierarchy.Parent = request.Collection.Parent;
            hierarchy.ItemsOrder = request.Collection.ItemsOrder;
            hierarchy.Slug = request.Collection.Slug;
            hierarchy.Type = isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
        }

        var convertedIIIF = request.RawRequestBody.ConvertToIIIFAndSetThumbnail(databaseCollection,
            request.Collection.PresentationThumbnail, logger);

        if (convertedIIIF.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();

        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        if (!isStorageCollection)
        {
            await bucketWriter.WriteToBucket(
                new ObjectInBucket(settings.AWS.S3.StorageBucket,
                    databaseCollection.GetCollectionBucketKey()),
                convertedIIIF.ConvertedCollection, "application/json", cancellationToken);
        }
        
        var items = dbContext
            .RetrieveCollectionItems(request.CustomerId, databaseCollection.Id)
            .Take(settings.PageSize);
        
        var total = await dbContext.GetTotalItemCountForCollection(databaseCollection, items.Count(), settings.PageSize, cancellationToken);

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
        
        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection, request.UrlRoots, settings.PageSize,
            DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken));

        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(enrichedPresentationCollection);
    }
}
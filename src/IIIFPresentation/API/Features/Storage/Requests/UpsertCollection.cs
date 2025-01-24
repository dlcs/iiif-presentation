using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using API.Settings;
using AWS.Helpers;
using Core;
using Core.Auth;
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

public class UpsertCollection(int customerId, string collectionId, PresentationCollection collection, string? eTag, 
    string rawRequestBody)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;

    public PresentationCollection Collection { get; } = collection;
    
    public string? ETag { get; } = eTag;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class UpsertCollectionHandler(
    PresentationContext dbContext,
    IETagManager eTagManager,
    ILogger<UpsertCollectionHandler> logger,
    IIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpsertCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(UpsertCollection request, 
        CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior!.IsStorageCollection();
        TryConvertIIIFResult<IIIF.Presentation.V3.Collection>? iiifCollection = null;
        if (!isStorageCollection)
        {
            iiifCollection = request.RawRequestBody.ConvertCollectionToIIIF<IIIF.Presentation.V3.Collection>(logger);
            if (iiifCollection.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        }
        var databaseCollection =
            await dbContext.RetrieveCollectionWithParentAsync(request.CustomerId, request.CollectionId, true, cancellationToken);

        Collection parentCollection;
        
        if (databaseCollection == null)
        {
            // No existing collection = create
            if (!string.IsNullOrEmpty(request.ETag)) return ErrorHelper.EtagNotRequired<PresentationCollection>();

            var createdDate = DateTime.UtcNow;
            
            parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                request.Collection.Parent.GetLastPathElement(), true, cancellationToken);
            
            if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();
            // If full URI was used, verify it indeed is pointing to the resolved parent collection
            if (request.Collection.IsUriParentInvalid(parentCollection, pathGenerator))
                return ErrorHelper.NullParentResponse<PresentationCollection>();

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
                Thumbnail = request.Collection.GetThumbnail(),
                Tags = request.Collection.Tags,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Type = isStorageCollection
                            ? ResourceType.StorageCollection
                            : ResourceType.IIIFCollection,
                        Slug = request.Collection.Slug,
                        Canonical = true,
                        ItemsOrder = request.Collection.ItemsOrder,
                        Parent = parentCollection.Id
                    }
                ]
            };
            
            await dbContext.AddAsync(databaseCollection, cancellationToken);
        }
        else
        {
            eTagManager.TryGetETag($"/{request.CustomerId}/collections/{request.CollectionId}", out var eTag);

            if (request.ETag != eTag) return ErrorHelper.EtagNonMatching<PresentationCollection>();
            
            if (isStorageCollection != databaseCollection.IsStorageCollection)
            {
                logger.LogError(
                    "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                    request.CustomerId, request.CollectionId, isStorageCollection ? "storage" : "iiif");
                return ErrorHelper.CannotChangeCollectionType<PresentationCollection>(isStorageCollection);
            }

            var existingHierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

            var parentId = existingHierarchy.Parent;
            if (parentId != request.Collection.Parent)
            {
                parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                    request.Collection.Parent.GetLastPathElement(), cancellationToken: cancellationToken);
                logger.LogDebug("Collection {CollectionId} for Customer {CustomerId} is moving parent",
                    request.CollectionId, request.CustomerId);

                if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

                // If full URI was used, verify it indeed is pointing to the resolved parent collection
                if (request.Collection.IsUriParentInvalid(parentCollection, pathGenerator)) 
                    return ErrorHelper.NullParentResponse<PresentationCollection>();

                parentId = parentCollection.Id;
            }
            else
            {
                parentCollection = existingHierarchy.ParentCollection!;
            }

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            databaseCollection.IsPublic = request.Collection.Behavior.IsPublic();
            databaseCollection.IsStorageCollection = isStorageCollection;
            databaseCollection.Label = request.Collection.Label;
            databaseCollection.Thumbnail = request.Collection.GetThumbnail();
            databaseCollection.Tags = request.Collection.Tags;

            existingHierarchy.Parent = parentId;
            existingHierarchy.ItemsOrder = request.Collection.ItemsOrder;
            existingHierarchy.Slug = request.Collection.Slug;
            existingHierarchy.Type = isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
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

        var hierarchy = databaseCollection.Hierarchy.Single();
        if (hierarchy.Parent != null)
        {
            try
            {
                databaseCollection.FullPath =
                    await CollectionRetrieval.RetrieveFullPathForCollection(databaseCollection, dbContext,
                        cancellationToken);
            }
            catch (PresentationException)
            {
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "New slug exceeds 1000 records.  This could mean an item no longer belongs to the root collection.",
                    ModifyCollectionType.PossibleCircularReference, WriteResult.BadRequest);
            }
        }
        
        await transaction.CommitAsync(cancellationToken);
        
        var items = dbContext
            .RetrieveCollectionItems(request.CustomerId, databaseCollection.Id)
            .Take(settings.PageSize);

        var total = await dbContext.GetTotalItemCountForCollection(databaseCollection, items.Count(),
            settings.PageSize, 1, cancellationToken);
        
        foreach (var item in items)
        {
            // We know the fullPath of parent collection so we can use that as the base for child items 
            item.FullPath = pathGenerator.GenerateFullPath(item, databaseCollection);
        }

        await UploadToS3IfRequiredAsync(databaseCollection, iiifCollection?.ConvertedIIIF, isStorageCollection,
            cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection,
            settings.PageSize, DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken),
            parentCollection, pathGenerator);

        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(enrichedPresentationCollection);
    }
    
    private async Task UploadToS3IfRequiredAsync(Collection collection, IIIF.Presentation.V3.Collection? iiifCollection, 
        bool isStorageCollection, CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, pathGenerator.GenerateFlatCollectionId(collection),
                cancellationToken);
        }
    }
}

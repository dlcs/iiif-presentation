using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using API.Infrastructure.AWS;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using API.Infrastructure.Validation;
using API.Settings;
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
    IIIFS3Service iiifS3,
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
            await dbContext.RetrieveCollectionAsync(request.CustomerId, request.CollectionId, true, cancellationToken);
        
        Hierarchy hierarchy;

        if (databaseCollection == null)
        {
            if (!string.IsNullOrEmpty(request.ETag)) return ErrorHelper.EtagNotRequired<PresentationCollection>();

            var createdDate = DateTime.UtcNow;
            
            var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                request.Collection.Parent.GetLastPathElement(), true, cancellationToken);
            
            if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();
            // If full URI was used, verify it indeed is pointing to the resolved parent collection
            if (request.Collection.IsUriParentInvalid(parentCollection, request.UrlRoots))
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

            if (request.ETag != eTag) return ErrorHelper.EtagNonMatching<PresentationCollection>();
            
            if (isStorageCollection != databaseCollection.IsStorageCollection)
            {
                logger.LogError(
                    "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                    request.CustomerId, request.CollectionId, isStorageCollection ? "storage" : "iiif");
                return ErrorHelper.CannotChangeCollectionType<PresentationCollection>(isStorageCollection);
            }

            hierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

            var parentId = hierarchy.Parent;
            if (hierarchy.Parent != request.Collection.Parent)
            {
                var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                    request.Collection.Parent.GetLastPathElement(), cancellationToken: cancellationToken);

                if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

                // If full URI was used, verify it indeed is pointing to the resolved parent collection
                if (request.Collection.IsUriParentInvalid(parentCollection, request.UrlRoots)) 
                    return ErrorHelper.NullParentResponse<PresentationCollection>();

                parentId = parentCollection.Id;
            }

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            databaseCollection.IsPublic = request.Collection.Behavior.IsPublic();
            databaseCollection.IsStorageCollection = isStorageCollection;
            databaseCollection.Label = request.Collection.Label;
            databaseCollection.Thumbnail = request.Collection.GetThumbnail();
            databaseCollection.Tags = request.Collection.Tags;

            hierarchy.Parent = parentId;
            hierarchy.ItemsOrder = request.Collection.ItemsOrder;
            hierarchy.Slug = request.Collection.Slug;
            hierarchy.Type = isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
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
        
        var total = await dbContext.GetTotalItemCountForCollection(databaseCollection, items.Count(), settings.PageSize, cancellationToken);
        
        foreach (var item in items)
        {
            // We know the fullPath of parent collection so we can use that as the base for child items 
            item.FullPath = item.GenerateFullPath(databaseCollection);
        }

        await UploadToS3IfRequiredAsync(databaseCollection, iiifCollection?.ConvertedIIIF, request.UrlRoots,
            isStorageCollection, cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection,
            request.UrlRoots, settings.PageSize, DefaultCurrentPage, total,
            await items.ToListAsync(cancellationToken: cancellationToken));

        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(enrichedPresentationCollection);
    }
    
    private async Task UploadToS3IfRequiredAsync(Collection collection, IIIF.Presentation.V3.Collection? iiifCollection, 
        UrlRoots urlRoots, bool isStorageCollection, CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, collection.GenerateFlatCollectionId(urlRoots),
                cancellationToken);
        }
    }
}
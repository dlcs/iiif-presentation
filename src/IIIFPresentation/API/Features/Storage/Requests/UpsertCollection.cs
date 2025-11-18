using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
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
using Repository.Paths;

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
    ILogger<UpsertCollectionHandler> logger,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IParentSlugParser parentSlugParser,
    IOptions<ApiSettings> options)
    : IRequestHandler<UpsertCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(UpsertCollection request, 
        CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        TryConvertIIIFResult<IIIF.Presentation.V3.Collection>? iiifCollection = null;
        if (!isStorageCollection)
        {
            iiifCollection = request.RawRequestBody.ConvertCollectionToIIIF<IIIF.Presentation.V3.Collection>(logger);
            if (iiifCollection.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        }
        var databaseCollection =
            await dbContext.RetrieveCollectionWithParentAsync(request.CustomerId, request.CollectionId, true, cancellationToken);

        var parsedParentSlugResult = await parentSlugParser.Parse(request.Collection, request.CustomerId,
            request.CollectionId, cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

        var parentCollection = parsedParentSlug.Parent;

        WriteResult result;
        if (databaseCollection == null)
        {
            // No existing collection = create
            result = WriteResult.Created;
            
            if (!string.IsNullOrEmpty(request.ETag)) return ErrorHelper.EtagNotRequired<PresentationCollection>();

            var createdDate = DateTime.UtcNow;

            databaseCollection = new Collection
            {
                Id = request.CollectionId,
                Created = createdDate,
                CreatedBy = Authorizer.GetUser(),
                CustomerId = request.CustomerId,
                Hierarchy =
                [
                    new Hierarchy
                    {
                        Type = isStorageCollection
                            ? ResourceType.StorageCollection
                            : ResourceType.IIIFCollection,
                        Slug = parsedParentSlug.Slug,
                        Canonical = true,
                        ItemsOrder = request.Collection.ItemsOrder,
                        Parent = parsedParentSlug.Parent.Id
                    }
                ]
            };
            
            SetCommonProperties(databaseCollection, request.Collection, createdDate);
            
            await dbContext.AddAsync(databaseCollection, cancellationToken);
        }
        else
        {
            // exists, so update
            result = WriteResult.Updated;
            
            if (!EtagComparer.IsMatch(databaseCollection.Etag, request.ETag))
                return ErrorHelper.EtagNonMatching<PresentationCollection>();
            
            if (isStorageCollection != databaseCollection.IsStorageCollection)
            {
                logger.LogError(
                    "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                    request.CustomerId, request.CollectionId, isStorageCollection ? "storage" : "iiif");
                return ErrorHelper.CannotChangeCollectionType<PresentationCollection>(isStorageCollection);
            }

            var existingHierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            SetCommonProperties(databaseCollection, request.Collection);

            // 'root' collection hierarchy can't change
            if (!databaseCollection.IsRoot())
            {
                existingHierarchy.Parent = parsedParentSlug.Parent.Id;
                existingHierarchy.ItemsOrder = request.Collection.ItemsOrder;
                existingHierarchy.Slug = request.Collection.Slug ?? string.Empty;
                existingHierarchy.Type =
                    isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
            }
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
                hierarchy.FullPath =
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
            item.FullPath = pathGenerator.GenerateFullPath(item, hierarchy);
        }

        await UploadToS3IfRequiredAsync(databaseCollection, iiifCollection?.ConvertedIIIF, isStorageCollection,
            cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection,
            settings.PageSize, DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken),
            parentCollection, pathGenerator);

        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(enrichedPresentationCollection, result, etag: databaseCollection.Etag);
    }

    /// <summary>
    /// Set properties that are common to both insert and update operations
    /// </summary>
    private static void SetCommonProperties(
        Collection databaseCollection, 
        PresentationCollection incomingCollection,
        DateTime? specificModifiedDate = null)
    {
        databaseCollection.Modified = specificModifiedDate ?? DateTime.UtcNow;
        databaseCollection.IsPublic = incomingCollection.Behavior.IsPublic();
        databaseCollection.IsStorageCollection = incomingCollection.Behavior.IsStorageCollection();
        databaseCollection.Label = incomingCollection.Label;
        databaseCollection.Thumbnail = incomingCollection.GetThumbnail();
        databaseCollection.Tags = incomingCollection.Tags;
    }
    
    private async Task UploadToS3IfRequiredAsync(Collection collection, IIIF.Presentation.V3.Collection? iiifCollection, 
        bool isStorageCollection, CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, pathGenerator.GenerateFlatCollectionId(collection),
                false, cancellationToken);
        }
    }
}

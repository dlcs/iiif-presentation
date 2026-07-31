using System.Data;
using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Helpers;
using Collection = Models.Database.Collections.Collection;

namespace API.Features.Storage;

/// <summary>
/// Class containing fields for Upserting a Collection
/// </summary>
public class UpsertCollectionRequest(
    string collectionId,
    string? etag,
    int customerId,
    PresentationCollection collection,
    string rawRequestBody,
    string? urlParentPath = null,
    string? urlSlug = null) : WriteCollectionRequest(customerId, collection, rawRequestBody, urlParentPath, urlSlug)
{
    public string CollectionId { get; } = collectionId;
    public string? ETag { get; } = etag;
}

/// <summary>
/// Class containing fields common to creating and upserting a Collection. Used directly for create requests, and
/// as the base for <see cref="UpsertCollectionRequest"/>, which adds the fields upsert needs on top
/// </summary>
public class WriteCollectionRequest(
    int customerId,
    PresentationCollection collection,
    string rawRequestBody,
    string? urlParentPath = null,
    string? urlSlug = null,
    string? clientProvidedId = null)
{
    public int CustomerId { get; } = customerId;
    public PresentationCollection Collection { get; } = collection;
    public string RawRequestBody { get; } = rawRequestBody;

    /// <summary>
    /// Parent path for the resource - the full path for hierarchical POST, everything but the last segment for
    /// hierarchical PUT, or derived from the request body's "id" property for a flat request (see
    /// <see cref="IRequestIdResolver"/>)
    /// </summary>
    public string? UrlParentPath { get; } = urlParentPath;

    /// <summary>
    /// Slug for the resource - the last segment of the path for hierarchical PUT, or derived from the request
    /// body's "id" property for a flat request
    /// </summary>
    public string? UrlSlug { get; } = urlSlug;

    /// <summary>
    /// A trusted, internal flat id resolved from the request body's "id" property (create only). When set, this is
    /// used as the new collection's id instead of minting a new one - the caller is responsible for having already
    /// recognised this as belonging to us (<see cref="IRequestIdResolver"/>); <see cref="CollectionWriteService.Create"/>
    /// still checks it isn't already in use
    /// </summary>
    public string? ClientProvidedId { get; } = clientProvidedId;
}

public interface ICollectionWrite
{
    /// <summary>
    /// Create or update full collection, using details provided in request object
    /// </summary>
    Task<PresentationResult> Upsert(UpsertCollectionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Create new collection, using details provided in request object
    /// </summary>
    Task<PresentationResult> Create(WriteCollectionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Service to help with creation and update of collections (storage or iiif)
/// </summary>
public class CollectionWriteService(
    PresentationContext dbContext,
    IdentityManager identityManager,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    SettingsBasedPathGenerator settingsBasedPathGenerator,
    IParentSlugParser parentSlugParser,
    IOptions<ApiSettings> options,
    ILogger<CollectionWriteService> logger) : ICollectionWrite
{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;

    /// <summary>
    /// Create new collection, using details provided in request object
    /// </summary>
    public async Task<PresentationResult> Create(WriteCollectionRequest request, CancellationToken cancellationToken)
    {
        string? id = null;
        if (request.ClientProvidedId != null)
        {
            var existing = await dbContext.RetrieveCollectionAsync(request.CustomerId, request.ClientProvidedId,
                cancellationToken: cancellationToken);
            if (existing != null) return UpsertErrorHelper.IdAlreadyExists();

            id = request.ClientProvidedId;
        }

        return await CreateInternal(request, id, cancellationToken);
    }

    /// <summary>
    /// Create or update full collection, using details provided in request object
    /// </summary>
    public async Task<PresentationResult> Upsert(UpsertCollectionRequest request, CancellationToken cancellationToken)
    {
        var databaseCollection =
            await dbContext.RetrieveCollectionWithParentAsync(request.CollectionId, true, cancellationToken);

        if (databaseCollection == null)
        {
            // No existing collection = create
            if (!string.IsNullOrEmpty(request.ETag)) return UpsertErrorHelper.EtagNotRequired();

            return await CreateInternal(request, request.CollectionId, cancellationToken);
        }

        return await UpdateInternal(request, databaseCollection, cancellationToken);
    }

    private async Task<PresentationResult> CreateInternal(WriteCollectionRequest request, string? id,
        CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        var (convertError, iiifCollection) = ConvertIfRequired(request.RawRequestBody, isStorageCollection);
        if (convertError != null) return convertError;

        var parsedParentSlugResult = await parentSlugParser.Parse(request.Collection, request.CustomerId, id,
            urlParentPath: request.UrlParentPath, urlSlug: request.UrlSlug, cancellationToken: cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

        if (id == null)
        {
            try
            {
                id = await identityManager.GenerateUniqueId<Collection>(request.CustomerId, cancellationToken);
            }
            catch (ConstraintException ex)
            {
                logger.LogError(ex, "An exception occured while generating a unique id");
                return UpsertErrorHelper.CannotGenerateUniqueId();
            }
        }

        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = id,
            Created = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            Hierarchy =
            [
                new Hierarchy
                {
                    Type = isStorageCollection
                        ? ResourceType.StorageCollection
                        : ResourceType.IIIFCollection,
                    Slug = parsedParentSlug!.Slug,
                    Canonical = true,
                    ItemsOrder = request.Collection.ItemsOrder,
                    Parent = parsedParentSlug.Parent!.Id
                }
            ]
        };
        SetCommonProperties(collection, request.Collection, dateCreated);

        dbContext.Collections.Add(collection);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);
        if (saveErrors != null) return saveErrors;

        try
        {
            collection.Hierarchy.GetCanonical().FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }
        catch (PresentationException)
        {
            return PresentationResult.Failure(
                "New slug exceeds 1000 records.  This could mean an item no longer belongs to the root collection.",
                ModifyCollectionType.PossibleCircularReference, WriteResult.BadRequest);
        }

        await transaction.CommitAsync(cancellationToken);

        await UploadToS3IfRequiredAsync(collection, iiifCollection, isStorageCollection, cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(collection,
            settings.PageSize, DefaultCurrentPage, 0, [], parsedParentSlug.Parent, pathGenerator,
            settingsBasedPathGenerator); // there can be no items attached to this, as it's just been created

        return PresentationResult.Success(enrichedPresentationCollection, WriteResult.Created, collection.Etag);
    }

    private async Task<PresentationResult> UpdateInternal(UpsertCollectionRequest request,
        Collection databaseCollection, CancellationToken cancellationToken)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        var (convertError, iiifCollection) = ConvertIfRequired(request.RawRequestBody, isStorageCollection);
        if (convertError != null) return convertError;

        var parsedParentSlugResult = await parentSlugParser.Parse(request.Collection, request.CustomerId,
            request.CollectionId, urlParentPath: request.UrlParentPath, urlSlug: request.UrlSlug,
            cancellationToken: cancellationToken);
        if (parsedParentSlugResult.IsError) return parsedParentSlugResult.Errors;
        var parsedParentSlug = parsedParentSlugResult.ParsedParentSlug;

        if (!EtagComparer.IsMatch(databaseCollection.Etag, request.ETag))
            return UpsertErrorHelper.EtagNonMatching();

        if (isStorageCollection != databaseCollection.IsStorageCollection)
        {
            logger.LogError(
                "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                request.CustomerId, request.CollectionId, isStorageCollection ? "storage" : "iiif");
            return UpsertErrorHelper.CannotChangeCollectionType(isStorageCollection);
        }

        var existingHierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

        databaseCollection.ModifiedBy = Authorizer.GetUser();
        SetCommonProperties(databaseCollection, request.Collection);

        // 'root' collection hierarchy can't change
        if (!databaseCollection.IsRoot())
        {
            existingHierarchy.Parent = parsedParentSlug.Parent!.Id;
            existingHierarchy.ItemsOrder = request.Collection.ItemsOrder;
            existingHierarchy.Slug = parsedParentSlug.Slug;
            existingHierarchy.Type =
                isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);
        if (saveErrors != null) return saveErrors;

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
                return PresentationResult.Failure(
                    "New slug exceeds 1000 records.  This could mean an item no longer belongs to the root collection.",
                    ModifyCollectionType.PossibleCircularReference, WriteResult.BadRequest);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var items = dbContext
            .RetrieveCollectionItems(databaseCollection.Id)
            .Take(settings.PageSize);

        var total = await dbContext.GetTotalItemCountForCollection(databaseCollection, items.Count(),
            settings.PageSize, 1, cancellationToken);

        foreach (var item in items)
        {
            // We know the fullPath of parent collection so we can use that as the base for child items
            item.FullPath = pathGenerator.GenerateFullPath(item, hierarchy);
        }

        await UploadToS3IfRequiredAsync(databaseCollection, iiifCollection, isStorageCollection, cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection,
            settings.PageSize, DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken),
            parsedParentSlug.Parent, pathGenerator, settingsBasedPathGenerator);

        return PresentationResult.Success(enrichedPresentationCollection, etag: databaseCollection.Etag);
    }

    private (PresentationResult? error, IIIF.Presentation.V3.Collection? iiifCollection) ConvertIfRequired(
        string rawRequestBody, bool isStorageCollection)
    {
        if (isStorageCollection) return (null, null);

        var converted = rawRequestBody.ConvertCollectionToIIIF(logger);
        return converted.Error ? (UpsertErrorHelper.CannotValidateIIIF(), null) : (null, converted.ConvertedIIIF);
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

    private async Task UploadToS3IfRequiredAsync(Collection collection,
        IIIF.Presentation.V3.Collection? iiifCollection, bool isStorageCollection,
        CancellationToken cancellationToken = default)
    {
        if (!isStorageCollection)
        {
            await iiifS3.SaveIIIFToS3(iiifCollection!, collection, pathGenerator.GenerateFlatCollectionId(collection),
                false, cancellationToken);
        }
    }
}

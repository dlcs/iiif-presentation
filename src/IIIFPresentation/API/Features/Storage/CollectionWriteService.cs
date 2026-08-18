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
    ResolvedLocation? location = null) : WriteCollectionRequest(customerId, collection, rawRequestBody, location)
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
    ResolvedLocation? location = null)
{
    public int CustomerId { get; } = customerId;
    public PresentationCollection Collection { get; } = collection;
    public string RawRequestBody { get; } = rawRequestBody;
    public ResolvedLocation Location { get; } = location ?? ResolvedLocation.None;
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
        if (request.Location.ClientProvidedId != null)
        {
            var existing = await dbContext.RetrieveCollectionAsync(request.CustomerId,
                request.Location.ClientProvidedId, cancellationToken: cancellationToken);
            if (existing != null) return UpsertErrorHelper.IdAlreadyExists();

            id = request.Location.ClientProvidedId;
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
        var (error, built) = await BuildCollectionForCreate(request, id, cancellationToken);
        if (error != null) return error;
        var collection = built!.Collection;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors = await dbContext.TrySave("collection", request.CustomerId, logger, cancellationToken);
        if (saveErrors != null) return saveErrors;

        var fullPathError = await TrySetFullPath(collection, collection.Hierarchy.GetCanonical(), cancellationToken);
        if (fullPathError != null) return fullPathError;

        await transaction.CommitAsync(cancellationToken);

        await UploadToS3IfRequiredAsync(collection, built.IiifCollection, collection.IsStorageCollection,
            cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(collection,
            settings.PageSize, DefaultCurrentPage, 0, [], built.ParsedParentSlug.Parent, pathGenerator,
            settingsBasedPathGenerator); // there can be no items attached to this, as it's just been created

        return PresentationResult.Success(enrichedPresentationCollection, WriteResult.Created, collection.Etag);
    }

    private async Task<(PresentationResult? error, BuiltCollection? built)> BuildCollectionForCreate(
        WriteCollectionRequest request, string? id, CancellationToken cancellationToken)
    {
        var prepared = PrepareCollection(request);
        if (prepared.Error != null) return (prepared.Error, null);

        var (slugError, parsedParentSlug) = await parentSlugParser.ParseParentSlug(request.Collection,
            request.CustomerId, id, request.Location, cancellationToken);
        if (slugError != null) return (slugError, null);

        if (id == null)
        {
            try
            {
                id = await identityManager.GenerateUniqueId<Collection>(request.CustomerId, cancellationToken);
            }
            catch (ConstraintException ex)
            {
                logger.LogError(ex, "An exception occured while generating a unique id");
                return (UpsertErrorHelper.CannotGenerateUniqueId(), null);
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
                    Type = prepared.IsStorageCollection
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

        return (null, new BuiltCollection(collection, prepared.IiifCollection, parsedParentSlug));
    }

    private async Task<PresentationResult> UpdateInternal(UpsertCollectionRequest request,
        Collection databaseCollection, CancellationToken cancellationToken)
    {
        var (error, built) = await BuildCollectionForUpdate(request, databaseCollection, cancellationToken);
        if (error != null) return error;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors = await dbContext.TrySave("collection", request.CustomerId, logger, cancellationToken);
        if (saveErrors != null) return saveErrors;

        var hierarchy = databaseCollection.Hierarchy!.Single();
        if (hierarchy.Parent != null)
        {
            var fullPathError = await TrySetFullPath(databaseCollection, hierarchy, cancellationToken);
            if (fullPathError != null) return fullPathError;
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

        await UploadToS3IfRequiredAsync(databaseCollection, built!.IiifCollection,
            databaseCollection.IsStorageCollection, cancellationToken);

        var enrichedPresentationCollection = request.Collection.EnrichPresentationCollection(databaseCollection,
            settings.PageSize, DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken),
            built.ParsedParentSlug.Parent, pathGenerator, settingsBasedPathGenerator);

        return PresentationResult.Success(enrichedPresentationCollection, etag: databaseCollection.Etag);
    }

    private async Task<(PresentationResult? error, BuiltCollection? built)> BuildCollectionForUpdate(
        UpsertCollectionRequest request, Collection databaseCollection, CancellationToken cancellationToken)
    {
        var prepared = PrepareCollection(request);
        if (prepared.Error != null) return (prepared.Error, null);

        var (slugError, parsedParentSlug) = await parentSlugParser.ParseParentSlug(request.Collection,
            request.CustomerId, request.CollectionId, request.Location, cancellationToken);
        if (slugError != null) return (slugError, null);

        if (!EtagComparer.IsMatch(databaseCollection.Etag, request.ETag))
            return (UpsertErrorHelper.EtagNonMatching(), null);

        if (prepared.IsStorageCollection != databaseCollection.IsStorageCollection)
        {
            logger.LogError(
                "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                request.CustomerId, request.CollectionId, prepared.IsStorageCollection ? "storage" : "iiif");
            return (UpsertErrorHelper.CannotChangeCollectionType(prepared.IsStorageCollection), null);
        }

        var existingHierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

        databaseCollection.ModifiedBy = Authorizer.GetUser();
        SetCommonProperties(databaseCollection, request.Collection);

        // 'root' collection hierarchy can't change
        if (!databaseCollection.IsRoot())
        {
            existingHierarchy.Parent = parsedParentSlug!.Parent!.Id;
            existingHierarchy.ItemsOrder = request.Collection.ItemsOrder;
            existingHierarchy.Slug = parsedParentSlug.Slug;
            existingHierarchy.Type =
                prepared.IsStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
        }

        return (null, new BuiltCollection(databaseCollection, prepared.IiifCollection, parsedParentSlug!));
    }

    /// <summary>
    /// Result of <see cref="BuildCollectionForCreate"/>/<see cref="BuildCollectionForUpdate"/> - the collection
    /// entity (tracked, not yet saved) together with the extra state the rest of the write needs once it's
    /// persisted.
    /// </summary>
    private record BuiltCollection(Collection Collection, IIIF.Presentation.V3.Collection? IiifCollection,
        ParsedParentSlug ParsedParentSlug);

    /// <summary>
    /// Recomputes and sets a collection's canonical hierarchy full path, converting the recursive-CTE's
    /// too-many-records guard into a client-facing error (a sign the parent chain no longer resolves to root -
    /// e.g. an ancestor was moved under one of its own descendants).
    /// </summary>
    private async Task<PresentationResult?> TrySetFullPath(Collection collection, Hierarchy hierarchy,
        CancellationToken cancellationToken)
    {
        try
        {
            hierarchy.FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
            return null;
        }
        catch (PresentationException)
        {
            return PresentationResult.Failure(
                "New slug exceeds 1000 records.  This could mean an item no longer belongs to the root collection.",
                ModifyCollectionType.PossibleCircularReference, WriteResult.BadRequest);
        }
    }

    /// <summary>
    /// Determines whether the request is for a storage collection and, if not, converts the raw body to plain IIIF
    /// </summary>
    private PreparedCollection PrepareCollection(WriteCollectionRequest request)
    {
        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        if (isStorageCollection) return PreparedCollection.Success(true, null);

        var converted = request.RawRequestBody.ConvertCollectionToIIIF(logger);
        return converted.Error
            ? PreparedCollection.Failure(UpsertErrorHelper.CannotValidateIIIF())
            : PreparedCollection.Success(false, converted.ConvertedIIIF);
    }

    /// <summary>
    /// Result of <see cref="PrepareCollection"/>
    /// </summary>
    private class PreparedCollection
    {
        public PresentationResult? Error { get; private init; }
        public bool IsStorageCollection { get; private init; }
        public IIIF.Presentation.V3.Collection? IiifCollection { get; private init; }

        public static PreparedCollection Failure(PresentationResult error) => new() { Error = error };

        public static PreparedCollection Success(bool isStorageCollection,
            IIIF.Presentation.V3.Collection? iiifCollection) =>
            new() { IsStorageCollection = isStorageCollection, IiifCollection = iiifCollection };
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

using System.Data;
using System.Diagnostics;
using API.Converters;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Features.Storage.Validators;
using API.Helpers;
using API.Infrastructure.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DBCollection = Models.Database.Collections.Collection;
using Result =
    API.Infrastructure.Requests.ModifyEntityResult<Models.API.Collection.PresentationCollection,
        Models.API.General.ModifyCollectionType>;

namespace API.Features.Storage.Requests;

/// <summary>
/// Common encapsulation of data available in a controller action, including the caller's intent
/// </summary>
/// <param name="customerId">Caller's customer id - assumed verified by authentication</param>
/// <param name="requestMethod">HTTP method used by caller, which implies intent (create at parent vs. upsert resource at location)</param>
/// <param name="requestPath">Entire path AFTER /{customerId} - might be flat, providing resource type and id, might be hierarchical showing parent and possibly the intended/desired slug</param>
/// <param name="rawRequestBody">Raw string data - will be parsed, validated etc.</param>
/// <param name="isHierarchical">Could be deduced from <paramref name="requestPath"/>, but this value is known by controller, so to simplify code it is provided explicitly</param>
/// <param name="isShowExtras">Whether the request included the "show extra properties" header</param>
///// <param name="initiatingController">Controller which created and sent this request. Used to create a response.</param>
public class CollectionWriteRequest(
    int customerId,
    HttpMethod requestMethod,
    string requestPath,
    string rawRequestBody,
    bool isHierarchical,
    bool isShowExtras,
    string? eTag
) : IRequest<Result>
{
    public int CustomerId { get; set; } = customerId;
    public HttpMethod RequestMethod { get; set; } = requestMethod;
    public string RequestPath { get; set; } = requestPath;
    public string RawRequestBody { get; set; } = rawRequestBody;
    public bool IsHierarchical { get; set; } = isHierarchical;
    public bool IsShowExtras { get; set; } = isShowExtras;
    public string? ETag { get; set; } = eTag;
}

public class CollectionWriteService(
    PresentationContext dbContext,
    ILogger<CollectionWriteService> logger,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IParentSlugParser parentSlugParser,
    IdentityManager identityManager,
    RootCollectionValidator rootValidator,
    PresentationValidator presentationValidator,
    IOptions<ApiSettings> options)
: IRequestHandler<CollectionWriteRequest, Result>

{
    private readonly ApiSettings settings = options.Value;

    private const int DefaultCurrentPage = 1;
    
    public async Task<Result> Handle(
        CollectionWriteRequest request, CancellationToken cancellationToken = default)
    {
        // This method should encapsulate all the rules, checks and actions in all scenarios where
        // a Collection (storage or not) is created or updated.
        
        // 1. Non-hierarchical requests must include the extra prop header
        // source: pre-existing CollectionController logic
        if (request is { IsHierarchical: false, IsShowExtras: false })
            return Result.Failure($"This request requires '{CustomHttpHeaders.ShowExtras}' header",
                ModifyCollectionType.ExtraHeaderRequired, WriteResult.Forbidden);

        // 2. Deserialize raw body as PresentationCollection
        var deserializedCollection =
            await request.RawRequestBody.TryDeserializePresentation<PresentationCollection>(logger);
        if (deserializedCollection.Error)
            return Result.Failure("Could not deserialize collection.", ModifyCollectionType.CannotDeserialize,
                WriteResult.BadRequest);
        
        var collection = deserializedCollection.ConvertedIIIF;
        Debug.Assert(collection != null, "collection != null"); // by validation above
        
        // 3. Find out if we have externally supplied `id`.
        // This can happen for flat PUT requests, and will be the last path segment
        string? suppliedId = null;
        if (!request.IsHierarchical && request.RequestMethod == HttpMethod.Put)
        {
            suppliedId = request.RequestPath.Split('/').Last();
        }

        // 4. Execute validator
        // source: pre-existing CollectionController logic
        // Note: only doing root validation, as the `PresentationValidator` is too narrow
        //       instead of that the various validations are done in this method

        if (suppliedId != null && KnownCollections.IsRoot(suppliedId))
        {
            var validation = await rootValidator.ValidateAsync(deserializedCollection.ConvertedIIIF, cancellationToken);
            if (!validation.IsValid)
            {
                var message = string.Join(". ", validation.Errors.Select(s => s.ErrorMessage).Distinct());
                return Result.Failure(message, ModifyCollectionType.ValidationFailed, WriteResult.FailedValidation);
            }
        }

        // 5. If not storage collection (i.e. "IIIF Collection"), ensure it's convertible
        // source: pre-existing logic in UpsertCollection/CreateCollection requests
        TryConvertIIIFResult<IIIF.Presentation.V3.Collection>? iiifCollection = null;

        var isStorageCollection = collection.Behavior.IsStorageCollection();
        if (!isStorageCollection)
        {
            iiifCollection = request.RawRequestBody.ConvertCollectionToIIIF<IIIF.Presentation.V3.Collection>(logger);
            if (iiifCollection.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        }

        // 6. If required, load a possibly-already-existing DB record of the collection
        // This is valid for PUT requests only
        // source: pre-existing logic in UpsertCollection
        DBCollection? databaseCollection = null;
        if (request.RequestMethod == HttpMethod.Put)
        {
            // 6.1 Ensure we have id of the collection. For flat PUTs this has been already loaded above (from path)
            var retrievalId = suppliedId;

            // For hierarchical calls we have to first inspect the hierarchy using the full hierarchical path
            if (request.IsHierarchical)
            {
                // RequestPath here is e.g. /grandparentStorage/parentStorage/collectionBeingUpserted
                var checkHierarchy =
                    await dbContext.RetrieveHierarchy(request.CustomerId, request.RequestPath, cancellationToken);
                retrievalId =
                    checkHierarchy
                        ?.CollectionId; // if hierarchy returned record AND it's a collection, then CollectionId is not null
            }

            if (retrievalId != null)
            {
                databaseCollection =
                    await dbContext.RetrieveCollectionWithParentAsync(request.CustomerId, retrievalId, true,
                        cancellationToken);
            }
        }

        // 7. Determine the operation (create/update)
        // source: pre-existing logic in UpsertCollection
        var result = databaseCollection == null ? WriteResult.Created : WriteResult.Updated;

        // 8. ETag check
        // For creation, ETag is forbidden.
        // For update, ETag is mandatory AND it MUST match
        if (databaseCollection == null)
        {
            if (!string.IsNullOrEmpty(request.ETag))
                return ErrorHelper.EtagNotRequired<PresentationCollection>();
        }
        else
        {
            if (!EtagComparer.IsMatch(databaseCollection.Etag, request.ETag))
                return ErrorHelper.EtagNonMatching<PresentationCollection>();
        }

        // 9. Determine the collection id
        // It can be the one we already have (in the retrieved db record) or we need to mint new one.
        // source: pre-existing logic in CreateCollection
        string collectionId;

        try
        {
            // Assumption: for a PUT into existing resource, db collection id is not null.
            // If it's a flat PUT to a desired "location", we have a "desired id".
            // All other cases are creation without id and call for a new id to be minted
            collectionId = databaseCollection?.Id ??
                           suppliedId ??
                           await identityManager.GenerateUniqueId<DBCollection>(request.CustomerId, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ErrorHelper.CannotGenerateUniqueId<PresentationCollection>();
        }

        // 10. Check for collection type conversion
        // If this is an update, we want to inform the user that conversion of collection type is not allowed
        // source: pre-existing logic in UpsertCollection
        if (databaseCollection != null && isStorageCollection != databaseCollection.IsStorageCollection)
        {
            logger.LogError(
                "Customer {CustomerId} attempted to convert collection {CollectionId} to {CollectionType}",
                request.CustomerId, databaseCollection.Id, isStorageCollection ? "storage" : "iiif");
            return ErrorHelper.CannotChangeCollectionType<PresentationCollection>(isStorageCollection);
        }

        // 11. Determine and validate the parent collection and the created/updated resource slug
        // This section is quite "varied", as this information can be passed in many different ways
        // based on both the URL, method and body JSON
        DBCollection? parent = null;
        string? resourceSlug;

        // Special case: updating root collection
        var isRootUpdate =
            request.RequestMethod == HttpMethod.Put // root is created automatically so it can only be updated
            && databaseCollection != null // it's guaranteed to already exist, so this will be true
            && KnownCollections.IsRoot(databaseCollection.Id);
        
        // For root update we don't need parent, and we have slug already
        if (isRootUpdate)
        {
            resourceSlug = databaseCollection!.Hierarchy!.Single().Slug;
        }
        else
        {
            // 11.1. Hierarchical
            if (request.IsHierarchical)
            {
                // Info/assumptions:
                // a. Parent provided hierarchical (grandparent/parent/desired-slug) [PUT]
                // b. Parent provided hierarchical (grandparent/parent) [POST]
                // c. Slug can be provided in body as id (grandparent/parent/desired-slug)
                // d. Can be Presentation Collection (with slug and or parent in body)
                // e. Body-supplied parent can be hierarchical OR flat
                // f. Primarily the parent AND slug are taken from the PUT url
                // g. Body id can contain desired hierarchical path, just like url
                // h. If mismatch between body and url: bad request

                string parentSlug;

                // In hierarchical call, RequestPath is slug (a) [PUT]
                if (request.RequestMethod == HttpMethod.Put)
                {
                    // PUT
                    var splitSlug = request.RequestPath.Split('/');
                    var resourceSlugFromUrl = splitSlug[^1];

                    // 11.1.1. Check: slug mismatch
                    if (collection.Slug is { Length: > 0 } bodySlug
                        && !string.Equals(resourceSlugFromUrl, bodySlug))
                    {
                        return ErrorHelper.MismatchedId<PresentationCollection>();
                    }

                    // if it wasn't set, do it here as "normalization" - this simplifies code later
                    collection.Slug ??= resourceSlugFromUrl;
                    parentSlug = string.Join("/", splitSlug.Take(..^1));
                }
                else
                {
                    // POST
                    parentSlug = request.RequestPath;

                    string? resourceSlugFromId = null;
                    if (collection.Id is { Length: > 0 })
                    {
                        // First, let's set the `PublicId` to the id
                        // this will let us use the ParentSlugParser, and we'll be minting an id anyway
                        collection.PublicId = collection.Id;

                        var parentSlugResult =
                            await parentSlugParser.Parse(collection, request.CustomerId, null, cancellationToken);

                        if (parentSlugResult.IsError)
                            return parentSlugResult.Errors;

                        parent = parentSlugResult.ParsedParentSlug.Parent;
                        resourceSlugFromId = parentSlugResult.ParsedParentSlug.Slug;

                        collection.Slug ??= resourceSlugFromId;
                    }

                    // Check: if slug provided in `id` AND in `slug`, they must match:
                    if (resourceSlugFromId != null)
                    {
                        // as we did ??=, this will fail only if there is indeed mismatch
                        if (!string.Equals(collection.Slug, resourceSlugFromId))
                            return ErrorHelper.MismatchedId<PresentationCollection>();
                    }
                }

                var parentCollection =
                    await dbContext.RetrieveHierarchy(request.CustomerId, parentSlug, cancellationToken);

                var parentValidationError =
                    ParentValidator.ValidateParentCollection<PresentationCollection>(parentCollection?.Collection);

                if (parentValidationError != null)
                    return parentValidationError;

                // by the above validation
                Debug.Assert(parentCollection != null, $"{nameof(parentCollection)} != null");

                // 11.1.2. Check: parent mismatch
                // -with `parent` variable? [this is clanky, but it's even worse otherwise]
                if (parent != null)
                {
                    if (!string.Equals(parent.Id, parentCollection.Collection?.Id))
                        return ErrorHelper.MismatchedParent<PresentationCollection>();
                }

                // -with `parent` property?
                if (collection.Parent is { Length: > 0 })
                {
                    var parseResult =
                        await parentSlugParser.Parse(collection, request.CustomerId, null, cancellationToken);

                    if (parseResult.IsError)
                        return parseResult.Errors;

                    if (parseResult.ParsedParentSlug?.Parent is { } parsedParent
                        && !string.Equals(parsedParent.Id, parentCollection.Collection?.Id))
                    {
                        return ErrorHelper.MismatchedParent<PresentationCollection>();
                    }
                }

                // 11.1.3: Set variables for use below
                parent = parentCollection.Collection;
                resourceSlug = collection.Slug;
            }
            else
            {
                // 11.2. Flat
                var parsedParentSlugResult = await parentSlugParser.Parse(collection, request.CustomerId,
                    collectionId, cancellationToken);
                if (parsedParentSlugResult.IsError)
                    return parsedParentSlugResult.Errors;

                // By above error check
                Debug.Assert(parsedParentSlugResult.ParsedParentSlug.Parent != null,
                    "parsedParentSlugResult.ParsedParentSlug.Parent != null");

                parent = parsedParentSlugResult.ParsedParentSlug.Parent;
                resourceSlug = parsedParentSlugResult.ParsedParentSlug.Slug;
            }
        }

        // By above
        if(!isRootUpdate)
        {
            Debug.Assert(parent != null, $"{nameof(parent)} != null");
        }
        Debug.Assert(resourceSlug != null, $"{nameof(resourceSlug)} != null");

        // finally check the slug against prohibited list
        // source: from PresentationValidator
        if(SpecConstants.ProhibitedSlugs.Contains(resourceSlug))
            return ErrorHelper.ProhibitedSlug<PresentationCollection>(resourceSlug);

        
        // 12. Create/update the db collection object
        // source: pre-existing logic in UpsertCollection
        if (databaseCollection == null)
        {
            var createdDate = DateTime.UtcNow;

            databaseCollection = new DBCollection
            {
                Id = collectionId,
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
                        Slug = resourceSlug,
                        Canonical = true,
                        ItemsOrder = collection.ItemsOrder,
                        Parent = parent.Id
                    }
                ]
            };
            
            SetCommonProperties(databaseCollection, collection, createdDate);
            
            await dbContext.AddAsync(databaseCollection, cancellationToken);
        }
        else
        {
            var existingHierarchy = databaseCollection.Hierarchy!.Single(c => c.Canonical);

            databaseCollection.Modified = DateTime.UtcNow;
            databaseCollection.ModifiedBy = Authorizer.GetUser();
            SetCommonProperties(databaseCollection, collection);

            // 'root' collection hierarchy can't change
            if (!databaseCollection.IsRoot())
            {
                existingHierarchy.Parent = parent.Id;
                existingHierarchy.ItemsOrder = collection.ItemsOrder;
                existingHierarchy.Slug = resourceSlug;
                existingHierarchy.Type =
                    isStorageCollection ? ResourceType.StorageCollection : ResourceType.IIIFCollection;
            }
        }
        
        // Above should ensure:
        Debug.Assert(databaseCollection != null, $"{nameof(databaseCollection)} != null");
        
        // 13. Save changes
        // source: pre-existing logic in UpsertCollection
         await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }

        var hierarchy = databaseCollection.Hierarchy!.Single();
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
                return Result.Failure(
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

        
        // If we want just plain IIIF output, we'll clean (i.e. rewrite the standard props) and return as-is.
        if (!request.IsShowExtras)
        {
            // Note: skipping "IsHierarchical" check because it has to be
            collection.Id = pathGenerator.GenerateHierarchicalId(hierarchy);
            return Result.Success(PresentationIIIFCleaner.OnlyIIIFProperties(collection), WriteResult.Created, databaseCollection.Etag);
        }
        
        var enrichedPresentationCollection = collection.EnrichPresentationCollection(databaseCollection,
            settings.PageSize, DefaultCurrentPage, total, await items.ToListAsync(cancellationToken: cancellationToken),
            parent, pathGenerator);

        if (request.IsHierarchical)
        {
            enrichedPresentationCollection.Id = pathGenerator.GenerateHierarchicalId(hierarchy);
        }

        return Result.Success(enrichedPresentationCollection, result, etag: databaseCollection.Etag);
    
    }
    
    /// <summary>
    /// Set properties that are common to both insert and update operations
    /// </summary>
    private static void SetCommonProperties(
        DBCollection databaseCollection, 
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

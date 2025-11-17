using System.Data;
using System.Diagnostics;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using MediatR;
using Models.API.Collection;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DatabaseCollection = Models.Database.Collections;

namespace API.Features.Storage.Requests;

public class PutHierarchicalCollection(
    int customerId,
    string slug, 
    string rawRequestBody) : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class PutHierarchicalCollectionHandler(
    PresentationContext dbContext,    
    ILogger<PutHierarchicalCollectionHandler> logger,
    IdentityManager identityManager,
    IIIIFS3Service iiifS3,
    IPathGenerator pathGenerator,
    IParentSlugParser parentSlugParser)
    : IRequestHandler<PutHierarchicalCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    
    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(PutHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        // Info:
        // 1. Parent provided hierarchical (grandparent/parent/desired-slug)
        // 2. Can be vanilla IIIF collection (no slug/parent in body)
        // 3. Can be Presentation Collection (with slug and or parent in body)
        // 4. Body-supplied parent can be hierarchical OR flat
        // 5. Primarily the parent AND slug are taken from the PUT url
        // 6. Body id can contain desired hierarchical path, just like url
        // 7. If mismatch between body and url: bad request
        
        
        var convertResult = await request.RawRequestBody.TryDeserializePresentation<PresentationCollection>(logger);
        if (convertResult.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        var collectionFromBody = convertResult.ConvertedIIIF!;
        
        var splitSlug = request.Slug.Split('/');
        var resourceSlugFromUrl = splitSlug[^1];
        
        // Check: slug mismatch
        if (collectionFromBody.Slug is { Length: > 0 } bodySlug && !string.Equals(resourceSlugFromUrl, bodySlug))
        {
            return ErrorHelper.MismatchedId<PresentationCollection>();
        }
        
        // if it wasn't set, do it here as "normalization" - this simplifies code later
        collectionFromBody.Slug ??= resourceSlugFromUrl;
        
        var parentSlug = string.Join("/", splitSlug.Take(..^1));
        var parentCollection =
            await dbContext.RetrieveHierarchy(request.CustomerId, parentSlug, cancellationToken);
        
        var parentValidationError =
            ParentValidator.ValidateParentCollection<PresentationCollection>(parentCollection?.Collection);
        if (parentValidationError != null) return parentValidationError;
        
        // by the above validation
        Debug.Assert(parentCollection != null, $"{nameof(parentCollection)} != null");
        
        // Check: parent mismatch
        if (collectionFromBody.Parent is { Length: > 0 })
        {
            var parseResult =
                await parentSlugParser.Parse(collectionFromBody, request.CustomerId, null, cancellationToken);

            if (parseResult.IsError)
                return parseResult.Errors;
            
            if(parseResult.ParsedParentSlug?.Parent is {} parsedParent
               && !string.Equals(parsedParent.Id, parentCollection.Collection?.Id))
            {
                return ErrorHelper.MismatchedId<PresentationCollection>();
            }
        }
        
        // TODO (Possibly): body `id` can contain the same path as URL-derived slug, but it's really only useful for POST - this is already complex, and the only reason to check the `id` here would be to verify it matches the PUT path
        
        var id = await GenerateUniqueId(request, cancellationToken);
        if (id == null) return ErrorHelper.CannotGenerateUniqueId<PresentationCollection>();

        var collection = CreateDatabaseCollection(request, collectionFromBody, id, parentCollection, splitSlug);
        dbContext.Collections.Add(collection);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        await iiifS3.SaveIIIFToS3(collectionFromBody, collection, pathGenerator.GenerateFlatCollectionId(collection),
            false, cancellationToken);

        var hierarchy = collection.Hierarchy.GetCanonical();
        
        if (hierarchy.Parent != null)
        {
            hierarchy.FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }

        collectionFromBody.Id = pathGenerator.GenerateHierarchicalId(hierarchy);
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(collectionFromBody, WriteResult.Created, collection.Etag);
    }

    private static DatabaseCollection.Collection CreateDatabaseCollection(PutHierarchicalCollection request, Collection collectionFromBody, string id,
        Hierarchy parentHierarchy, string[] splitSlug)
    {
        var thumbnails = collectionFromBody.Thumbnail?.OfType<Image>().ToList();    
        
        var dateCreated = DateTime.UtcNow;
        var collection = new DatabaseCollection.Collection
        {
            Id = id,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            IsPublic = collectionFromBody.Behavior != null && collectionFromBody.Behavior.IsPublic(),
            IsStorageCollection = false,
            Label = collectionFromBody.Label,
            Thumbnail = thumbnails?.GetThumbnailPath(),
            Hierarchy = [
                new Hierarchy
                {
                    CollectionId = id,
                    Type = ResourceType.IIIFCollection,
                    Slug = splitSlug.Last(),
                    CustomerId = request.CustomerId,
                    Canonical = true,
                    ItemsOrder = 0,
                    Parent = parentHierarchy.CollectionId
                }
            ]
        };
        
        return collection;
    }

    private async Task<string?> GenerateUniqueId(PutHierarchicalCollection request, CancellationToken cancellationToken)
    {
        try
        {
            return await identityManager.GenerateUniqueId<DatabaseCollection.Collection>(request.CustomerId, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return null;
        }
    }
}

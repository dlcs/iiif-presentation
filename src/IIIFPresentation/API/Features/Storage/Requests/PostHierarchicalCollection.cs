using System.Data;
using API.Features.Common.Helpers;
using API.Features.Storage.Helpers;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests;
using AWS.Helpers;
using Core;
using Core.Auth;
using Core.IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using MediatR;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;
using DatabaseCollection = Models.Database.Collections;

namespace API.Features.Storage.Requests;

public class PostHierarchicalCollection(
    int customerId,
    string slug, 
    string rawRequestBody) : IRequest<ModifyEntityResult<Collection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class PostHierarchicalCollectionHandler(
    PresentationContext dbContext,    
    ILogger<PostHierarchicalCollectionHandler> logger,
    IdentityManager identityManager,
    IIIFS3Service iiifS3,
    IPathGenerator pathGenerator)
    : IRequestHandler<PostHierarchicalCollection, ModifyEntityResult<Collection, ModifyCollectionType>>
{
    
    public async Task<ModifyEntityResult<Collection, ModifyCollectionType>> Handle(PostHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var convertResult = request.RawRequestBody.ConvertCollectionToIIIF<Collection>(logger);
        if (convertResult.Error) return ErrorHelper.CannotValidateIIIF<Collection>();
        var collectionFromBody = convertResult.ConvertedIIIF!;
        
        var splitSlug = request.Slug.Split('/');

        var parentSlug = string.Join("/", splitSlug.Take(..^1));
        var parentCollection =
            await dbContext.RetrieveHierarchy(request.CustomerId, parentSlug, cancellationToken);
        
        var parentValidationError =
            ParentValidator.ValidateParentCollection<Collection>(parentCollection?.Collection);
        if (parentValidationError != null) return parentValidationError;
        
        var id = await GenerateUniqueId(request, cancellationToken);
        if (id == null) return ErrorHelper.CannotGenerateUniqueId<Collection>();

        var collection = CreateDatabaseCollection(request, collectionFromBody, id, parentCollection, splitSlug);
        dbContext.Collections.Add(collection);

        var saveErrors =
            await dbContext.TrySaveCollection<Collection>(request.CustomerId, logger,
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
        return ModifyEntityResult<Collection, ModifyCollectionType>.Success(collectionFromBody, WriteResult.Created);
    }

    private static DatabaseCollection.Collection CreateDatabaseCollection(PostHierarchicalCollection request, Collection collectionFromBody, string id,
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

    private async Task<string?> GenerateUniqueId(PostHierarchicalCollection request, CancellationToken cancellationToken)
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

using System.Data;
using API.Auth;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using Core;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.General;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using DatabaseCollection = Models.Database.Collections;
using IIdGenerator = API.Infrastructure.IdGenerator.IIdGenerator;

namespace API.Features.Storage.Requests;

public class PostHierarchicalCollection(
    int customerId,
    string slug, 
    UrlRoots urlRoots,
    string rawRequestBody) : IRequest<ModifyEntityResult<Collection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
    
    public UrlRoots UrlRoots { get; } = urlRoots;
    
    public string RawRequestBody { get; } = rawRequestBody;
}

public class PostHierarchicalCollectionHandler(
    PresentationContext dbContext,    
    ILogger<PostHierarchicalCollectionHandler> logger,
    IBucketWriter bucketWriter,
    IIdGenerator idGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<PostHierarchicalCollection, ModifyEntityResult<Collection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;
    
    public async Task<ModifyEntityResult<Collection, ModifyCollectionType>> Handle(PostHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var collectionFromBody = BuildIIIFCollection(request);
        if (collectionFromBody == null) return ErrorHelper.CannotValidateIIIF<Collection>();
        
        var splitSlug = request.Slug.Split('/');

        var parentSlug = string.Join("/", splitSlug.Take(..^1));
        var parentCollection =
            await dbContext.RetrieveHierarchy(request.CustomerId, parentSlug, cancellationToken);
        
        if (parentCollection == null) return ErrorHelper.NullParentResponse<Collection>();
        
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
        
        await bucketWriter.WriteToBucket(
            new ObjectInBucket(settings.AWS.S3.StorageBucket,
                collection.GetResourceBucketKey()),
            collectionFromBody.AsJson(), "application/json", cancellationToken);
        
        if (collection.Hierarchy!.Single(h => h.Canonical).Parent != null)
        {
            collection.FullPath = await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }

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

    private Collection? BuildIIIFCollection(PostHierarchicalCollection request)
    {
        Collection? collection = null;
        try
        {
            collection = request.RawRequestBody.FromJson<Collection>();
            collection.Id = request.GetCollectionId();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while attempting to validate the collection as IIIF");
        }

        return collection;
    }

    private async Task<string?> GenerateUniqueId(PostHierarchicalCollection request, CancellationToken cancellationToken)
    {
        string? id = null;
        try
        {
            id = await dbContext.Collections.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
        }
        
        return id;
    }
}
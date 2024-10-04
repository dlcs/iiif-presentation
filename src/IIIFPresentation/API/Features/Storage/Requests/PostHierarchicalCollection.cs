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
using Newtonsoft.Json;
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
    ILogger<CreateCollection> logger,
    IBucketWriter bucketWriter,
    IIdGenerator idGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<PostHierarchicalCollection, ModifyEntityResult<Collection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;
    
    public async Task<ModifyEntityResult<Collection, ModifyCollectionType>> Handle(PostHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var splitSlug = request.Slug.Split('/');

        var parentSlug = String.Join(String.Empty, splitSlug.Take(..^1));
        var parentCollection =
            await dbContext.RetriveHierarchicalCollection(request.CustomerId, parentSlug, cancellationToken);
        
        if (parentCollection == null) return ErrorHelper.NullParentResponse<Collection>();

        string id;
        
        try
        {
            id = await dbContext.Collections.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ErrorHelper.CannotGenerateUniqueId<Collection>();
        }

        Collection collectionFromBody;
        try
        {
            collectionFromBody = request.RawRequestBody.FromJson<Collection>();
            collectionFromBody.Id = $"{request.UrlRoots.BaseUrl}/{request.CustomerId}/{request.Slug}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error attempting to validate collection is IIIF");
            return ErrorHelper.CannotValidateIIIF<Collection>();
        }
        
        var thumbnails = collectionFromBody.Thumbnail?.Select(CastAsClass<Image,ExternalResource>).ToList();        
        
        var dateCreated = DateTime.UtcNow;
        var collection = new DatabaseCollection.Collection
        {
            Id = id,
            Parent = parentCollection.Id,
            Slug = splitSlug.Last(),
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            IsPublic = collectionFromBody.Behavior != null && collectionFromBody.Behavior.IsPublic(),
            IsStorageCollection = false,
            Label = collectionFromBody.Label,
            Thumbnail = thumbnails?.GetThumbnailPath()
        };
        
        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
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
                $"{request.CustomerId}/collections/{collection.Id}"),
            collectionFromBody.AsJson(), "application/json", cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);

        if (collection.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }

        return ModifyEntityResult<Collection, ModifyCollectionType>.Success(collectionFromBody, WriteResult.Created);
    }

    private TNewClass CastAsClass<TNewClass, TExistingClass>(TExistingClass existingClass) where TNewClass : class
    {

        var newObject = Activator.CreateInstance<TNewClass>();
        var newProps = typeof(TNewClass).GetProperties();

        foreach (var prop in newProps)
        {
            if (!prop.CanWrite) continue;

            var existingPropertyInfo = typeof(TExistingClass).GetProperty(prop.Name);
            if (existingPropertyInfo == null || !existingPropertyInfo.CanRead) continue;
            var value = existingPropertyInfo.GetValue(existingClass);

            prop.SetValue(newObject, value, null);
        }

        return newObject;
    }
}
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
using Core.Helpers;
using IIIF.Presentation.V3.Content;
using IIIF.Serialisation;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.Collection.Upsert;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using IIdGenerator = API.Infrastructure.IdGenerator.IIdGenerator;

namespace API.Features.Storage.Requests;

public class CreateCollection(int customerId, UpsertFlatCollection collection, string rawRequestBody, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public UpsertFlatCollection Collection { get; } = collection;
    
    public string RawRequestBody { get; } = rawRequestBody;

    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class CreateCollectionHandler(
    PresentationContext dbContext,
    ILogger<CreateCollectionHandler> logger,
    IBucketWriter bucketWriter,
    IIdGenerator idGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<CreateCollection, ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    private readonly ApiSettings settings = options.Value;

    private const int CurrentPage = 1;
    
    public async Task<ModifyEntityResult<PresentationCollection, ModifyCollectionType>> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        // check parent exists
        var parentCollection = await dbContext.RetrieveCollection(request.CustomerId,
            request.Collection.Parent.GetLastPathElement(), cancellationToken);

        if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();
        
        string id;

        try
        {
            id = await dbContext.Collections.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ErrorHelper.CannotGenerateUniqueId<PresentationCollection>();
        }

        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = id,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            Tags = request.Collection.Tags,
            IsPublic = request.Collection.Behavior.IsPublic(),
            IsStorageCollection = request.Collection.Behavior.IsStorageCollection(),
            Label = request.Collection.Label
        };

        var hierarchy = new Hierarchy
        {
            CollectionId = id,
            Type = request.Collection.Behavior.IsStorageCollection()
                ? ResourceType.StorageCollection
                : ResourceType.IIIFCollection,
            Slug = request.Collection.Slug,
            CustomerId = request.CustomerId,
            Canonical = true,
            ItemsOrder = request.Collection.ItemsOrder,
            Parent = request.Collection.Parent,
            Public = request.Collection.Behavior.IsPublic()
        };
        
        string? convertedIIIFCollection = null;

        if (!request.Collection.Behavior.IsStorageCollection())
        {
            try
            {
                convertedIIIFCollection = ConvertToIIIFCollection(request, collection);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while attempting to validate the collection as IIIF");
                return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
            }
        }
        else
        {
            collection.Thumbnail = request.Collection.PresentationThumbnail;
        }
        
        dbContext.Collections.Add(collection);
        dbContext.Hierarchy.Add(hierarchy);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        await UploadToS3IfRequiredAsync(request.Collection, collection, convertedIIIFCollection!, cancellationToken);

        if (hierarchy.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }
        
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, CurrentPage, 0, []), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }

    private static string ConvertToIIIFCollection(CreateCollection request, Collection collection)
    {
        var collectionAsIIIF = request.RawRequestBody.FromJson<IIIF.Presentation.V3.Collection>();
        var convertedIIIFCollection = collectionAsIIIF.AsJson();
        var thumbnails = collectionAsIIIF.Thumbnail?.OfType<Image>().ToList();
        if (thumbnails != null)
        {
            collection.Thumbnail = thumbnails.GetThumbnailPath();
        }
        return convertedIIIFCollection;
    }

    private async Task UploadToS3IfRequiredAsync(UpsertFlatCollection request,
        Collection collection, string convertedIIIFCollection, CancellationToken cancellationToken)
    {
        if (!request.Behavior.IsStorageCollection())
        {
            await bucketWriter.WriteToBucket(
                new ObjectInBucket(settings.AWS.S3.StorageBucket,
                    collection.GetCollectionBucketKey()),
                convertedIIIFCollection, "application/json", cancellationToken);
        }
    }
}
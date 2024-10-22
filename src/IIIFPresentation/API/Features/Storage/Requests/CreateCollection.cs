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
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
using Models.Database.Collections;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using IIdGenerator = API.Infrastructure.IdGenerator.IIdGenerator;

namespace API.Features.Storage.Requests;

public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody, UrlRoots urlRoots)
    : IRequest<ModifyEntityResult<PresentationCollection, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection? Collection { get; } = collection;
    
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
        var parentCollection = await dbContext.RetrieveCollectionAsync(request.CustomerId,
            request.Collection!.Parent.GetLastPathElement(), cancellationToken: cancellationToken);

        if (parentCollection == null) return ErrorHelper.NullParentResponse<PresentationCollection>();

        var isStorageCollection = request.Collection.Behavior.IsStorageCollection();
        
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
            IsStorageCollection = isStorageCollection,
            Label = request.Collection.Label
        };

        var hierarchy = new Hierarchy
        {
            CollectionId = id,
            Type = isStorageCollection
                ? ResourceType.StorageCollection
                : ResourceType.IIIFCollection,
            Slug = request.Collection.Slug,
            CustomerId = request.CustomerId,
            Canonical = true,
            ItemsOrder = request.Collection.ItemsOrder,
            Parent = request.Collection.Parent
        };

        var convertedIIIF =
            request.RawRequestBody.ConvertToIIIFAndSetThumbnail(collection, request.Collection.PresentationThumbnail,
                logger);

        if (convertedIIIF.Error) return ErrorHelper.CannotValidateIIIF<PresentationCollection>();
        
        dbContext.Collections.Add(collection);
        dbContext.Hierarchy.Add(hierarchy);

        var saveErrors =
            await dbContext.TrySaveCollection<PresentationCollection>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        if (!isStorageCollection)
        {
            await bucketWriter.WriteToBucket(
                new ObjectInBucket(settings.AWS.S3.StorageBucket,
                    collection.GetCollectionBucketKey()),
                convertedIIIF.ConvertedCollection, "application/json", cancellationToken);
        }

        if (hierarchy.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }
        
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            request.Collection.EnrichFlatCollection(collection, request.UrlRoots, settings.PageSize, CurrentPage, 0, []), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }
}
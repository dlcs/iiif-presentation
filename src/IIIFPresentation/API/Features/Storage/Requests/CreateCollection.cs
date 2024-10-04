using System.Text;
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
using Models.API.Collection.Upsert;
using Models.API.General;
using Models.Database.Collections;
using Newtonsoft.Json;
using NuGet.Protocol;
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
    ILogger<CreateCollection> logger,
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

        if (parentCollection == null)
        {
            return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                $"The parent collection could not be found", ModifyCollectionType.ParentCollectionNotFound,
                WriteResult.Conflict);
        }
        
        string id;

        try
        {
            id = await dbContext.Collections.GenerateUniqueIdAsync(request.CustomerId, idGenerator, cancellationToken);
        }
        catch (ConstraintException ex)
        {
            logger.LogError(ex, "An exception occured while generating a unique id");
            return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                "Could not generate a unique identifier.  Please try again",
                ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
        }

        var dateCreated = DateTime.UtcNow;
        var collection = new Collection
        {
            Id = id,
            Created = dateCreated,
            Modified = dateCreated,
            CreatedBy = Authorizer.GetUser(),
            CustomerId = request.CustomerId,
            IsPublic = request.Collection.Behavior.IsPublic(),
            IsStorageCollection = request.Collection.Behavior.IsStorageCollection(),
            Label = request.Collection.Label,
            Parent = parentCollection.Id,
            Slug = request.Collection.Slug,
            Thumbnail = request.Collection.Thumbnail,
            Tags = request.Collection.Tags,
            ItemsOrder = request.Collection.ItemsOrder
        };
        
        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        dbContext.Collections.Add(collection);

        var saveErrors = await dbContext.TrySaveCollection(request.CustomerId, logger, cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        if (!request.Collection.Behavior.IsStorageCollection())
        {
            try
            {
                var collectionToSave = GenerateCollectionFromRawRequest(request.RawRequestBody);

                await bucketWriter.WriteToBucket(
                    new ObjectInBucket(settings.AWS.S3.StorageBucket,
                        $"{request.CustomerId}/collections/{collection.Id}"),
                    collectionToSave, "application/json", cancellationToken);
            }
            catch (JsonSerializationException ex)
            {
                logger.LogError(ex, "Error attempting to validate collection is IIIF");
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "Error attempting to validate collection is IIIF", ModifyCollectionType.CannotValidateIIIF,
                    WriteResult.BadRequest);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unknown exception occured while creating a new collection");
                return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Failure(
                    "Unknown error occured while creating a collection", ModifyCollectionType.Unknown,
                    WriteResult.Error);
            }
        }
        
        await transaction.CommitAsync(cancellationToken);

        if (collection.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }
        
        return ModifyEntityResult<PresentationCollection, ModifyCollectionType>.Success(
            collection.ToFlatCollection(request.UrlRoots, settings.PageSize, CurrentPage, 0, []), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }

    private static string GenerateCollectionFromRawRequest(string rawRequestBody)
    {
        var collection = rawRequestBody.FromJson<IIIF.Presentation.V3.Collection>();
        return collection.ToJson();
    }
}
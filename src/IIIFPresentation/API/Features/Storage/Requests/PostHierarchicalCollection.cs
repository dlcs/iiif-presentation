using System.Data;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.S3;
using AWS.S3.Models;
using Core;
using IIIF.Presentation.V3;
using MediatR;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Models.API.General;
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

        if (parentCollection == null)
        {
            return ModifyEntityResult<Collection, ModifyCollectionType>.Failure(
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
            return ModifyEntityResult<Collection, ModifyCollectionType>.Failure(
                "Could not generate a unique identifier.  Please try again",
                ModifyCollectionType.CannotGenerateUniqueId, WriteResult.Error);
        }

        var collection = new DatabaseCollection.Collection
        {
            Id = id,
            Parent = parentCollection.Id,
            Slug = splitSlug.Last(),
        };
        
        await using var transaction = 
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        dbContext.Collections.Add(collection);

        var saveErrors =
            await dbContext.TrySaveCollection<Collection, ModifyCollectionType>(request.CustomerId, logger,
                cancellationToken);

        if (saveErrors != null)
        {
            return saveErrors;
        }
        
        await bucketWriter.WriteToBucket(
            new ObjectInBucket(settings.AWS.S3.StorageBucket,
                $"{request.CustomerId}/collections/{splitSlug.Last()}"),
            request.RawRequestBody, "application/json", cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);

        if (collection.Parent != null)
        {
            collection.FullPath = CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext);
        }

        return ModifyEntityResult<Collection, ModifyCollectionType>.Success(
            collection.ToHierarchicalCollection(request.UrlRoots, []), // there can be no items attached to this, as it's just been created
            WriteResult.Created);
    }
}
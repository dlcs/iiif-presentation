using API.Features.Storage.Models;
using API.Helpers;
using AWS.S3;
using AWS.S3.Models;
using AWS.Settings;
using Core.Streams;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Database.Collections;
using Repository;
using Repository.Helpers;

namespace API.Features.Storage.Requests;

public class GetHierarchicalCollection(int customerId, string slug) : IRequest<CollectionWithItems>
{
    public int CustomerId { get; } = customerId;

    public string Slug { get; } = slug;
}

public class GetHierarchicalCollectionHandler(PresentationContext dbContext, IBucketReader bucketReader, 
    IOptions<AWSSettings> options)
    : IRequestHandler<GetHierarchicalCollection, CollectionWithItems>
{
    private readonly AWSSettings settings = options.Value;
    
    public async Task<CollectionWithItems> Handle(GetHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        var collection =
            await dbContext.RetriveHierarchicalCollection(request.CustomerId, request.Slug, cancellationToken);

        List<Collection>? items = null;
        string? collectionFromS3 = null;

        if (collection != null)
        {
            if (!collection.IsStorageCollection)
            {
                var objectFromS3 = await bucketReader.GetObjectFromBucket(new ObjectInBucket(settings.S3.StorageBucket,
                    collection.GetCollectionBucketKey()), cancellationToken);

                if (!objectFromS3.Stream.IsNull())
                {
                    using var reader = new StreamReader(objectFromS3.Stream);
                    collectionFromS3 = await reader.ReadToEndAsync(cancellationToken);
                }
            }
            else
            {
                items = await dbContext.Collections
                    .Where(s => s.CustomerId == request.CustomerId && s.Parent == collection.Id)
                    .ToListAsync(cancellationToken: cancellationToken);

                items.ForEach(item => item.FullPath = collection.GenerateFullPath(item.Slug));
            }

            collection.FullPath = request.Slug;
        }

        return new CollectionWithItems(collection, items, items?.Count ?? 0, collectionFromS3);
    }
}
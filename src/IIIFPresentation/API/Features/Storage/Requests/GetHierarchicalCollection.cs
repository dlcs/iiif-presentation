using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using AWS.Helpers;
using AWS.S3;
using AWS.S3.Models;
using AWS.Settings;
using Core.Streams;
using IIIF.Presentation.V3;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Repository.Paths;

namespace API.Features.Storage.Requests;

public class GetHierarchicalCollection(Hierarchy hierarchy)
    : IRequest<CollectionWithItems>
{
    public Hierarchy Hierarchy { get; } = hierarchy;
}

public class GetHierarchicalCollectionHandler(
    PresentationContext dbContext, 
    IBucketReader bucketReader, 
    IPathGenerator pathGenerator,
    IOptions<AWSSettings> options,
    ILogger<GetHierarchicalCollectionHandler> logger)
    : IRequestHandler<GetHierarchicalCollection, CollectionWithItems>
{
    private readonly AWSSettings settings = options.Value;
    
    public async Task<CollectionWithItems> Handle(GetHierarchicalCollection request,
        CancellationToken cancellationToken)
    {
        if (request.Hierarchy.CollectionId == null || request.Hierarchy.Collection == null)
        {
            logger.LogWarning("Attempt to fetch collection for '{FullPath}' but hierarchy has null collection",
                request.Hierarchy.FullPath);
            return CollectionWithItems.Empty;
        }

        if (!request.Hierarchy.Collection.IsPublic) return CollectionWithItems.Empty;

        if (request.Hierarchy.Type != ResourceType.StorageCollection)
        {
            var objectFromS3 = await bucketReader.GetObjectFromBucket(new ObjectInBucket(settings.S3.StorageBucket,
                request.Hierarchy.Collection!.GetResourceBucketKey()), cancellationToken);

            if (!objectFromS3.Stream.IsNull())
            {
                var collectionFromS3 =
                    objectFromS3.GetDescriptionResourceWithId<Collection>(
                        pathGenerator.GenerateHierarchicalId(request.Hierarchy));
                return new(request.Hierarchy.Collection, null, 0, collectionFromS3);
            }
        }
        else
        {
            var items = await dbContext
                .RetrieveCollectionItems(request.Hierarchy.CustomerId, request.Hierarchy.Collection.Id, true)
                .ToListAsync(cancellationToken: cancellationToken);

            // The incoming slug will be the base, use that to generate child item path
            items.ForEach(item => item.FullPath = pathGenerator.GenerateFullPath(item, request.Hierarchy.FullPath));
            
            return new(request.Hierarchy.Collection, items, items.Count);
        }

        return CollectionWithItems.Empty;
    }
}

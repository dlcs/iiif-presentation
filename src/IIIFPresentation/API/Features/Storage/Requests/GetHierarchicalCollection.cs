﻿using API.Features.Storage.Helpers;
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
using Models.Database.General;
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
        var hierarchy =
            await dbContext.RetrieveHierarchy(request.CustomerId, request.Slug, cancellationToken);
        List<Collection>? items = null;
        string? collectionFromS3 = null;

        if (hierarchy?.CollectionId != null)
        {
            if (hierarchy.Type != ResourceType.StorageCollection)
            {
                var objectFromS3 = await bucketReader.GetObjectFromBucket(new ObjectInBucket(settings.S3.StorageBucket,
                    hierarchy.Collection!.GetCollectionBucketKey()), cancellationToken);

                if (!objectFromS3.Stream.IsNull())
                {
                    using var reader = new StreamReader(objectFromS3.Stream);
                    collectionFromS3 = await reader.ReadToEndAsync(cancellationToken);
                }
            }
            else
            {
                if (hierarchy.Collection != null)
                {
                    items = await dbContext.RetrieveCollectionItems(request.CustomerId, hierarchy.Collection.Id)
                        .ToListAsync(cancellationToken: cancellationToken);

                    items.ForEach(item => item.FullPath = hierarchy.GenerateFullPath(item.Hierarchy!.Single(h => h.Canonical).Slug));

                    hierarchy.Collection.FullPath = request.Slug;
                }
            }
        }

        return new CollectionWithItems(hierarchy?.Collection, hierarchy, items, items?.Count ?? 0, collectionFromS3);
    }
}
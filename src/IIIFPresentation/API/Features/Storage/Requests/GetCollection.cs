using System.Collections.Immutable;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using AWS.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Models.API.Collection;
using Repository;
using Repository.Collections;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace API.Features.Storage.Requests;

public class GetCollection(
    int customerId,
    string id,
    IImmutableSet<Guid> eTags,
    int page,
    int pageSize,
    string? orderBy = null,
    bool descending = false) : IRequest<FetchEntityResult<PresentationCollection>>
{
    public int CustomerId { get; } = customerId;

    public string Id { get; } = id;

    public IImmutableSet<Guid> IfNoneMatch { get; } = eTags;

    public RequestModifiers RequestModifiers { get; } = new()
    {
        PageSize = pageSize,
        Page = page,
        OrderBy = orderBy,
        Descending = descending
    };
}

public class GetCollectionHandler(PresentationContext dbContext, IIIIFS3Service iiifS3, IPathGenerator pathGenerator, 
    SettingsBasedPathGenerator settingsBasedPathGenerator) 
    : IRequestHandler<GetCollection, FetchEntityResult<PresentationCollection>>
{
    public async Task<FetchEntityResult<PresentationCollection>> Handle(GetCollection request,
        CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionWithParentAsync(request.CustomerId, request.Id,
            cancellationToken: cancellationToken);

        if (collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        if (request.IfNoneMatch.Contains(collection.Etag))
            return FetchEntityResult<PresentationCollection>.Matched(collection.Etag);

        var hierarchy = collection.Hierarchy.GetCanonical();

        var parentCollection = collection.Hierarchy?.SingleOrDefault()?.ParentCollection;

        var orderByParameter = request.RequestModifiers.OrderBy != null
            ? $"{(request.RequestModifiers.Descending ? "orderByDescending" : "orderBy")}={request.RequestModifiers.OrderBy}"
            : null;

        if (hierarchy.Parent != null)
        {
            collection.Hierarchy.GetCanonical().FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }

        if (collection.IsStorageCollection)
        {
            var items = await dbContext.RetrieveCollectionItems(request.CustomerId, collection.Id)
                .AsOrderedCollectionItemsQuery(request.RequestModifiers.OrderBy, request.RequestModifiers.Descending)
                .Skip((request.RequestModifiers.Page - 1) * request.RequestModifiers.PageSize)
                .Take(request.RequestModifiers.PageSize)
                .ToListAsync(cancellationToken: cancellationToken);

            var total = await dbContext.GetTotalItemCountForCollection(collection, items.Count,
                request.RequestModifiers.PageSize,
                request.RequestModifiers.Page, cancellationToken);

            // We know the fullPath of parent collection so we can use that as the base for child items
            items.ForEach(item =>
                item.FullPath = pathGenerator.GenerateFullPath(item, hierarchy));

            var presentationCollection = collection.ToPresentationCollection(request.RequestModifiers.PageSize,
                request.RequestModifiers.Page, total, items, parentCollection, pathGenerator,
                settingsBasedPathGenerator, orderByParameter);

            return FetchEntityResult<PresentationCollection>.Success(presentationCollection, collection.Etag);
        }

        var s3Collection =
            await iiifS3.ReadIIIFFromS3<PresentationCollection>(collection.GetResourceBucketKey(),
                cancellationToken);

        if (s3Collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        var s3PresentationCollection =
            s3Collection.SetIIIFGeneratedFields(collection, parentCollection, pathGenerator,
                settingsBasedPathGenerator);

        return FetchEntityResult<PresentationCollection>.Success(s3PresentationCollection, collection.Etag);
    }
}

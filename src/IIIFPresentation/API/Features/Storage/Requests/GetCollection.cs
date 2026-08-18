using System.Collections.Immutable;
using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Infrastructure.Requests;
using API.Settings;
using AWS.Helpers;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Repository;
using Repository.Collections;
using Repository.Helpers;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace API.Features.Storage.Requests;

public class GetCollection(
    string id,
    IImmutableSet<Guid> eTags,
    int? page,
    int? pageSize,
    string? orderBy = null,
    bool descending = false) : IRequest<FetchEntityResult<PresentationCollection>>, IPagedRequest
{
    public string Id { get; } = id;

    public IImmutableSet<Guid> IfNoneMatch { get; } = eTags;

    public int? Page { get; } = page;
    public int? PageSize { get; } = pageSize;
    public string? OrderBy { get; } = orderBy;
    public bool Descending { get; } = descending;
}

public class GetCollectionHandler(PresentationContext dbContext, IIIIFS3Service iiifS3, IPathGenerator pathGenerator, 
    SettingsBasedPathGenerator settingsBasedPathGenerator, IOptions<ApiSettings> options) 
    : IRequestHandler<GetCollection, FetchEntityResult<PresentationCollection>>
{
    public async ValueTask<FetchEntityResult<PresentationCollection>> Handle(GetCollection request,
        CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionWithParentAsync(request.Id,
            cancellationToken: cancellationToken);

        if (collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        if (request.IfNoneMatch.Contains(collection.Etag))
        {
            return FetchEntityResult<PresentationCollection>.Matched(collection.Etag);
        }

        var hierarchy = collection.Hierarchy.GetCanonical();

        var parentCollection = collection.Hierarchy?.SingleOrDefault()?.ParentCollection;

        if (hierarchy.Parent != null)
        {
            collection.Hierarchy.GetCanonical().FullPath =
                await CollectionRetrieval.RetrieveFullPathForCollection(collection, dbContext, cancellationToken);
        }

        if (collection.IsStorageCollection)
        {
            var requestModifiers = request.GetRequestModifiers(options.Value);
            
            var items = await dbContext.RetrieveCollectionItems(collection.Id)
                .AsOrderedCollectionItemsQuery(requestModifiers.OrderBy, requestModifiers.Descending)
                .Skip((requestModifiers.Page - 1) * requestModifiers.PageSize)
                .Take(requestModifiers.PageSize)
                .ToListAsync(cancellationToken: cancellationToken);

            var total = await dbContext.GetTotalItemCountForCollection(collection, items.Count,
                requestModifiers.PageSize,
                requestModifiers.Page, cancellationToken);

            // We know the fullPath of parent collection so we can use that as the base for child items
            items.ForEach(item =>
                item.FullPath = pathGenerator.GenerateFullPath(item, hierarchy));

            var presentationCollection = collection.ToPresentationCollection(requestModifiers.PageSize,
                requestModifiers.Page, total, items, parentCollection, pathGenerator,
                settingsBasedPathGenerator, requestModifiers.GetOrderByParameter());

            return FetchEntityResult<PresentationCollection>.Success(presentationCollection, collection.Etag);
        }

        var s3Collection =
            await iiifS3.ReadIIIFFromS3<PresentationCollection>(collection, BucketLocationType.Default,
                cancellationToken);

        if (s3Collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        var s3PresentationCollection =
            s3Collection.SetIIIFGeneratedFields(collection, parentCollection, pathGenerator,
                settingsBasedPathGenerator);

        return FetchEntityResult<PresentationCollection>.Success(s3PresentationCollection, collection.Etag);
    }
}

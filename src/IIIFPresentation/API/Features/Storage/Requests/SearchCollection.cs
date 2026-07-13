using API.Converters;
using API.Features.Storage.Helpers;
using API.Features.Storage.Models;
using API.Infrastructure.Requests;
using API.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Models.API.Collection;
using Repository;
using Repository.Collections;
using Repository.Paths;
using Services.Manifests.Helpers;

namespace API.Features.Storage.Requests;

public class SearchCollection(string id, string label, int? page, int? pageSize)
    : IRequest<FetchEntityResult<PresentationCollection>>, IPagedRequest
{
    public string Id { get; } = id;

    public string Label { get; } = label;

    public int? Page { get; } = page;

    public int? PageSize { get; } = pageSize;

    // Search does not expose ordering yet (see RFC 0008); results use the default order.
    public string? OrderBy => null;

    public bool Descending => false;
}

public class SearchCollectionHandler(
    PresentationContext dbContext,
    IPathGenerator pathGenerator,
    SettingsBasedPathGenerator settingsBasedPathGenerator,
    IOptions<ApiSettings> options)
    : IRequestHandler<SearchCollection, FetchEntityResult<PresentationCollection>>
{
    public async Task<FetchEntityResult<PresentationCollection>> Handle(SearchCollection request,
        CancellationToken cancellationToken)
    {
        var collection = await dbContext.RetrieveCollectionWithParentAsync(request.Id,
            cancellationToken: cancellationToken);

        if (collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        // Only storage collections can be searched - their items are db records, whereas an IIIF collection's items
        // are stored in S3. Currently unreachable as callers restrict search to 'root', but the guard belongs here.
        if (!collection.IsStorageCollection)
            return FetchEntityResult<PresentationCollection>.Invalid("Search is only supported for storage collections");

        var parentCollection = collection.Hierarchy?.SingleOrDefault()?.ParentCollection;

        var searchQuery = dbContext.SearchCollectionItems(request.Label);

        var total = await searchQuery.CountAsync(cancellationToken);

        var requestModifiers = request.GetRequestModifiers(options.Value);
        var items = await searchQuery
            .AsOrderedCollectionItemsQuery(requestModifiers.OrderBy, requestModifiers.Descending)
            .Skip((requestModifiers.Page - 1) * requestModifiers.PageSize)
            .Take(requestModifiers.PageSize)
            .ToListAsync(cancellationToken);

        // Results can sit at any depth, so items are identified by their flat id (see RFC 0008) - no full path
        // is required
        var presentationCollection = collection.ToSearchCollection(request.Label, requestModifiers.PageSize,
            requestModifiers.Page, total, items, parentCollection, pathGenerator, settingsBasedPathGenerator,
            requestModifiers.GetOrderByParameter());

        // no etag - it's the collection's, and would be unchanged by results changing beneath it
        return FetchEntityResult<PresentationCollection>.Success(presentationCollection);
    }
}

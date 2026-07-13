using System.Diagnostics;
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
    IOptions<ApiSettings> options,
    ILogger<SearchCollectionHandler> logger)
    : IRequestHandler<SearchCollection, FetchEntityResult<PresentationCollection>>
{
    private readonly ApiSettings settings = options.Value;

    public async Task<FetchEntityResult<PresentationCollection>> Handle(SearchCollection request,
        CancellationToken cancellationToken)
    {
        // Only the collection itself is required - the search result is synthetic, so it carries none of the
        // collection's hierarchy (slug, parent, publicId etc)
        var collection = await dbContext.Collections.Retrieve(request.Id, cancellationToken: cancellationToken);

        if (collection is null) return FetchEntityResult<PresentationCollection>.NotFound();

        // Only storage collections can be searched - their items are db records, whereas an IIIF collection's items
        // are stored in S3. Currently unreachable as callers restrict search to 'root', but the guard belongs here.
        if (!collection.IsStorageCollection)
            return FetchEntityResult<PresentationCollection>.Invalid("Search is only supported for storage collections");

        var searchQuery = dbContext.SearchCollectionItems(request.Label);

        // Label search is an un-indexed ILIKE scan (see RFC 0008), so time the 2 queries it runs separately - the
        // count has no LIMIT to short-circuit it, so is expected to be the more expensive of the pair
        var countStopwatch = Stopwatch.StartNew();
        var total = await searchQuery.CountAsync(cancellationToken);
        countStopwatch.Stop();

        var requestModifiers = request.GetRequestModifiers(settings);

        var pageStopwatch = Stopwatch.StartNew();
        var items = await searchQuery
            .AsOrderedCollectionItemsQuery(requestModifiers.OrderBy, requestModifiers.Descending)
            .Skip((requestModifiers.Page - 1) * requestModifiers.PageSize)
            .Take(requestModifiers.PageSize)
            .ToListAsync(cancellationToken);
        pageStopwatch.Stop();

        LogSearchTimings(request, requestModifiers, total, countStopwatch.ElapsedMilliseconds,
            pageStopwatch.ElapsedMilliseconds);

        // Results can sit at any depth, so items are identified by their flat id (see RFC 0008) - no full path
        // is required
        var presentationCollection = collection.ToSearchCollection(request.Label, requestModifiers.PageSize,
            requestModifiers.Page, total, items, pathGenerator, requestModifiers.GetOrderByParameter());

        // no etag - it's the collection's, and would be unchanged by results changing beneath it
        return FetchEntityResult<PresentationCollection>.Success(presentationCollection);
    }

    /// <summary>
    /// Logs how long the search queries took, as a warning if over <see cref="ApiSettings.SlowSearchThresholdMs"/> so
    /// that slow searches surface without Debug logging enabled
    /// </summary>
    private void LogSearchTimings(SearchCollection request, RequestModifiers requestModifiers, int total,
        long countElapsed, long pageElapsed)
    {
        var level = countElapsed + pageElapsed >= settings.SlowSearchThresholdMs ? LogLevel.Warning : LogLevel.Debug;

        logger.Log(level,
            "Searched {CollectionId} for '{SearchTerm}' (count {CountElapsed}ms, page {PageElapsed}ms). {TotalMatches} matches page {Page} of {PageSize}",
            request.Id, request.Label, countElapsed, pageElapsed, total, requestModifiers.Page,
            requestModifiers.PageSize);
    }
}

using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Upsert Collection in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class UpsertCollection(int customerId, string collectionId, PresentationCollection collection, string? eTag,
    string rawRequestBody, string? urlParentPath = null, string? urlSlug = null)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;

    public PresentationCollection Collection { get; } = collection;

    public string? ETag { get; } = eTag;

    public string RawRequestBody { get; } = rawRequestBody;

    /// <summary>
    /// Parent path for the resource
    /// </summary>
    public string? UrlParentPath { get; } = urlParentPath;

    /// <summary>
    /// Slug for the resource
    /// </summary>
    public string? UrlSlug { get; } = urlSlug;
}

public class UpsertCollectionHandler(ICollectionWrite collectionService)
    : IRequestHandler<UpsertCollection, PresentationResult>
{
    public Task<PresentationResult> Handle(UpsertCollection request, CancellationToken cancellationToken)
    {
        var upsertRequest = new UpsertCollectionRequest(request.CollectionId, request.ETag, request.CustomerId,
            request.Collection, request.RawRequestBody, request.UrlParentPath, request.UrlSlug);

        return collectionService.Upsert(upsertRequest, cancellationToken);
    }
}

using API.Helpers;
using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Upsert Collection in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class UpsertCollection(int customerId, string collectionId, PresentationCollection collection, string? eTag,
    string rawRequestBody) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    public string CollectionId { get; } = collectionId;

    public PresentationCollection Collection { get; } = collection;

    public string? ETag { get; } = eTag;

    public string RawRequestBody { get; } = rawRequestBody;
}

public class UpsertCollectionHandler(ICollectionWrite collectionService, IRequestIdResolver requestIdResolver)
    : IRequestHandler<UpsertCollection, PresentationResult>
{
    public async Task<PresentationResult> Handle(UpsertCollection request, CancellationToken cancellationToken)
    {
        var (error, resolvedId) = requestIdResolver.ResolveAndValidate(request.CustomerId, request.Collection.Id,
            request.CollectionId);
        if (error != null) return error;

        var upsertRequest = new UpsertCollectionRequest(request.CollectionId, request.ETag, request.CustomerId,
            request.Collection, request.RawRequestBody, resolvedId.ToLocation());

        return await collectionService.Upsert(upsertRequest, cancellationToken);
    }
}

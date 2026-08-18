using API.Helpers;
using API.Infrastructure.Requests;
using Mediator;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create a new Collection (storage or iiif) in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection Collection { get; } = collection;

    public string RawRequestBody { get; } = rawRequestBody;
}

public class CreateCollectionHandler(ICollectionWrite collectionService, IRequestIdResolver requestIdResolver)
    : IRequestHandler<CreateCollection, PresentationResult>
{
    public async ValueTask<PresentationResult> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        var (error, resolvedId) = requestIdResolver.ResolveAndValidate(request.CustomerId, request.Collection.Id);
        if (error != null) return error;

        var writeRequest = new WriteCollectionRequest(request.CustomerId, request.Collection, request.RawRequestBody,
            resolvedId.ToLocation());

        return await collectionService.Create(writeRequest, cancellationToken);
    }
}

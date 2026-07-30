using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create a new Collection (storage or iiif) in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody,
    string? urlParentPath = null, string? clientProvidedId = null)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection Collection { get; } = collection;

    public string RawRequestBody { get; } = rawRequestBody;

    /// <summary>
    /// Hierarchical parent path derived from the request URL - set only for hierarchical POST, where the whole
    /// path being POSTed into is the parent container for the new resource
    /// </summary>
    public string? UrlParentPath { get; } = urlParentPath;

    /// <summary>
    /// A trusted, internal flat id resolved from the request body's "id" property
    /// </summary>
    public string? ClientProvidedId { get; } = clientProvidedId;
}

public class CreateCollectionHandler(ICollectionWrite collectionService)
    : IRequestHandler<CreateCollection, PresentationResult>
{
    public Task<PresentationResult> Handle(CreateCollection request, CancellationToken cancellationToken)
    {
        var writeRequest = new WriteCollectionRequest(request.CustomerId, request.Collection, request.RawRequestBody,
            request.UrlParentPath, clientProvidedId: request.ClientProvidedId);

        return collectionService.Create(writeRequest, cancellationToken);
    }
}

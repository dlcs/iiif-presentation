using API.Infrastructure.Requests;
using MediatR;
using Models.API.Collection;

namespace API.Features.Storage.Requests;

/// <summary>
/// Create a new Collection (storage or iiif) in DB and upload provided JSON to S3 if iiif-collection
/// </summary>
public class CreateCollection(int customerId, PresentationCollection collection, string rawRequestBody,
    string? urlParentPath = null, string? urlSlug = null, string? clientProvidedId = null)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;

    public PresentationCollection Collection { get; } = collection;

    public string RawRequestBody { get; } = rawRequestBody;

    /// <summary>
    /// Parent path for the new resource - the whole path being POSTed into for hierarchical POST, or derived from
    /// the request body's "id" property when it resolves to an own-host hierarchical id (flat POST)
    /// </summary>
    public string? UrlParentPath { get; } = urlParentPath;

    /// <summary>
    /// Slug derived from the request body's "id" property, when it resolves to an own-host hierarchical id
    /// </summary>
    public string? UrlSlug { get; } = urlSlug;

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
            request.UrlParentPath, urlSlug: request.UrlSlug, clientProvidedId: request.ClientProvidedId);

        return collectionService.Create(writeRequest, cancellationToken);
    }
}

using API.Infrastructure.Requests;
using MediatR;
using Models.API.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Create a new Manifest in DB and upload provided JSON to S3
/// </summary>
public class CreateManifest(
    int customerId,
    PresentationManifest presentationManifest,
    string rawRequestBody,
    bool createSpace,
    string? urlParentPath = null,
    string? urlSlug = null,
    string? clientProvidedId = null)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public bool CreateSpace { get; } = createSpace;

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

public class CreateManifestHandler(
    IManifestWrite manifestService) : IRequestHandler<CreateManifest,
    PresentationResult>
{
    public Task<PresentationResult> Handle(CreateManifest request,
        CancellationToken cancellationToken)
    {
        var upsertRequest = new WriteManifestRequest(request.CustomerId,
            request.PresentationManifest.RemoveInvalidPipelines(), // Necessary, makes downstream handling simpler
            request.RawRequestBody,
            request.CreateSpace,
            request.UrlParentPath,
            urlSlug: request.UrlSlug,
            clientProvidedId: request.ClientProvidedId);

        return manifestService.Create(upsertRequest, cancellationToken);
    }
}

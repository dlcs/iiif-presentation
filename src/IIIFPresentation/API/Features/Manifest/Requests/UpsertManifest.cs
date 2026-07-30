using API.Infrastructure.Requests;
using MediatR;
using Microsoft.Extensions.Primitives;
using Models.API.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Upsert Manifest in DB and upload provided JSON to S3
/// </summary>
public class UpsertManifest(
    int customerId,
    string manifestId,
    StringValues etag,
    PresentationManifest presentationManifest,
    string rawRequestBody,
    bool createSpace,
    string? urlParentPath = null,
    string? urlSlug = null) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;
    public string ManifestId { get; } = manifestId;
    public string? Etag { get; } = etag.ToString();
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public bool CreateSpace { get; } = createSpace;

    /// <summary>
    /// Hierarchical parent path derived from the request URL - set only for hierarchical PUT (everything but the
    /// last segment of the path)
    /// </summary>
    public string? UrlParentPath { get; } = urlParentPath;

    /// <summary>
    /// Slug derived from the request URL - set only for hierarchical PUT (the last segment of the path)
    /// </summary>
    public string? UrlSlug { get; } = urlSlug;
}

public class UpsertManifestHandler(IManifestWrite manifestService)
    : IRequestHandler<UpsertManifest, PresentationResult>
{
    public Task<PresentationResult> Handle(UpsertManifest request,
        CancellationToken cancellationToken)
    {
        var upsertRequest = new UpsertManifestRequest(
            request.ManifestId,
            request.Etag,
            request.CustomerId,
            request.PresentationManifest.RemoveInvalidPipelines(), // Necessary, makes downstream handling simpler
            request.RawRequestBody,
            request.CreateSpace,
            request.UrlParentPath,
            request.UrlSlug);

        return manifestService.Upsert(upsertRequest, cancellationToken);
    }
}

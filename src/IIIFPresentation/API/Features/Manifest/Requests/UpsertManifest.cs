using API.Helpers;
using API.Infrastructure.Requests;
using Mediator;
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
    bool createSpace) : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;
    public string ManifestId { get; } = manifestId;
    public string? Etag { get; } = etag.ToString();
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public bool CreateSpace { get; } = createSpace;
}

public class UpsertManifestHandler(IManifestWrite manifestService, IRequestIdResolver requestIdResolver)
    : IRequestHandler<UpsertManifest, PresentationResult>
{
    public async ValueTask<PresentationResult> Handle(UpsertManifest request, CancellationToken cancellationToken)
    {
        var (error, resolvedId) = requestIdResolver.ResolveAndValidate(request.CustomerId,
            request.PresentationManifest.Id, request.ManifestId);
        if (error != null) return error;

        var upsertRequest = new UpsertManifestRequest(
            request.ManifestId,
            request.Etag,
            request.CustomerId,
            request.PresentationManifest.RemoveInvalidPipelines(), // Necessary, makes downstream handling simpler
            request.RawRequestBody,
            request.CreateSpace,
            resolvedId.ToLocation());

        return await manifestService.Upsert(upsertRequest, cancellationToken);
    }
}

using API.Converters;
using API.Infrastructure.Requests;
using MediatR;
using Microsoft.Extensions.Primitives;
using Models.API.General;
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
    UrlRoots urlRoots) : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;
    public string ManifestId { get; } = manifestId;
    public string? Etag { get; } = etag.ToString();
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class UpsertManifestHandler(ManifestService manifestService)
    : IRequestHandler<UpsertManifest, ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>> Handle(UpsertManifest request,
        CancellationToken cancellationToken)
    {
        var upsertRequest = new UpsertManifestRequest(
            request.ManifestId,
            request.Etag,
            request.CustomerId,
            request.PresentationManifest,
            request.RawRequestBody,
            request.UrlRoots);

        return manifestService.Upsert(upsertRequest, cancellationToken);
    }
}
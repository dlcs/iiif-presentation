using API.Helpers;
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
    bool createSpace)
    : IRequest<PresentationResult>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public bool CreateSpace { get; } = createSpace;
}

public class CreateManifestHandler(IManifestWrite manifestService, IRequestIdResolver requestIdResolver)
    : IRequestHandler<CreateManifest, PresentationResult>
{
    public async Task<PresentationResult> Handle(CreateManifest request, CancellationToken cancellationToken)
    {
        var (error, resolvedId) = requestIdResolver.ResolveAndValidate(request.CustomerId,
            request.PresentationManifest.Id);
        if (error != null) return error;

        var upsertRequest = new WriteManifestRequest(request.CustomerId,
            request.PresentationManifest.RemoveInvalidPipelines(), // Necessary, makes downstream handling simpler
            request.RawRequestBody,
            request.CreateSpace,
            resolvedId.ToLocation());

        return await manifestService.Create(upsertRequest, cancellationToken);
    }
}

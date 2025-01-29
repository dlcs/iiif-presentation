using API.Infrastructure.Requests;
using MediatR;
using Models.API.General;
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
    : IRequest<ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public int CustomerId { get; } = customerId;
    public PresentationManifest PresentationManifest { get; } = presentationManifest;
    public string RawRequestBody { get; } = rawRequestBody;
    public bool CreateSpace { get; } = createSpace;
}

public class CreateManifestHandler(
    IManifestWrite manifestService) : IRequestHandler<CreateManifest,
    ModifyEntityResult<PresentationManifest, ModifyCollectionType>>
{
    public Task<ModifyEntityResult<PresentationManifest, ModifyCollectionType>> Handle(CreateManifest request,
        CancellationToken cancellationToken)
    {
        var upsertRequest = new WriteManifestRequest(request.CustomerId, 
            request.PresentationManifest,
            request.RawRequestBody,
            request.CreateSpace);

        return manifestService.Create(upsertRequest, cancellationToken);
    }
}

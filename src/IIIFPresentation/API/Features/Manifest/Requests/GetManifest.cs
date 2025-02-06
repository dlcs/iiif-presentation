using API.Infrastructure.Requests;
using MediatR;
using Models.API.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Attempt to read manifest from underlying storage
/// </summary>
public class GetManifest(
    int customerId,
    string id,
    bool pathOnly) : IRequest<FetchEntityResult<PresentationManifest>>
{
    public int CustomerId { get; } = customerId;
    public string Id { get; } = id;
    public bool PathOnly { get; } = pathOnly;
}

public class GetManifestHandler(IManifestRead manifestRead) : 
    IRequestHandler<GetManifest, FetchEntityResult<PresentationManifest>>
{
    public Task<FetchEntityResult<PresentationManifest>> Handle(GetManifest request,
        CancellationToken cancellationToken)
            => manifestRead.GetManifest(request.CustomerId, request.Id, request.PathOnly, cancellationToken);
}

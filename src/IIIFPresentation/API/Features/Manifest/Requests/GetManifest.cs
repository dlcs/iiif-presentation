using System.Collections.Immutable;
using API.Infrastructure.Helpers;
using API.Infrastructure.Requests;
using Mediator;
using Microsoft.Extensions.Primitives;
using Models.API.Manifest;

namespace API.Features.Manifest.Requests;

/// <summary>
/// Attempt to read manifest from underlying storage
/// </summary>
public class GetManifest(
    int customerId,
    string id,
    IImmutableSet<Guid> eTags,
    bool pathOnly) : IRequest<FetchEntityResult<PresentationManifest>>
{
    public int CustomerId { get; } = customerId;
    public string Id { get; } = id;
    public bool PathOnly { get; } = pathOnly;

    public IImmutableSet<Guid> IfNoneMatch { get; } = eTags;
}

public class GetManifestHandler(IManifestRead manifestRead) :
    IRequestHandler<GetManifest, FetchEntityResult<PresentationManifest>>
{
    public ValueTask<FetchEntityResult<PresentationManifest>> Handle(GetManifest request,
        CancellationToken cancellationToken)
        => new(manifestRead.GetManifest(request.CustomerId, request.Id, request.IfNoneMatch, request.PathOnly,
            cancellationToken));
}

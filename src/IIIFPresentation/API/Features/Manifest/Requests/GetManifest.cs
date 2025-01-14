﻿using API.Converters;
using API.Features.Storage.Helpers;
using API.Helpers;
using API.Infrastructure.Requests;
using AWS.Helpers;
using MediatR;
using Models.API.Manifest;
using Repository;
using Repository.Helpers;

namespace API.Features.Manifest.Requests;

public class GetManifest(
    int customerId,
    string id,
    bool pathOnly) : IRequest<FetchEntityResult<PresentationManifest>>
{
    public int CustomerId { get; } = customerId;
    public string Id { get; } = id;
    public bool PathOnly { get; } = pathOnly;
}

public class GetManifestHandler(
    PresentationContext dbContext,
    IPathGenerator pathGenerator,
    IIIFS3Service iiifS3) : IRequestHandler<GetManifest, FetchEntityResult<PresentationManifest>>
{
    public async Task<FetchEntityResult<PresentationManifest>> Handle(GetManifest request,
        CancellationToken cancellationToken)
    {
        var dbManifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.Id, cancellationToken: cancellationToken);

        if (dbManifest == null) return FetchEntityResult<PresentationManifest>.NotFound();

        var fetchFullPath = ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId,
            dbContext, cancellationToken);

        if (request.PathOnly)
            return FetchEntityResult<PresentationManifest>.Success(new()
            {
                FullPath = pathGenerator.GenerateHierarchicalFromFullPath(request.CustomerId, await fetchFullPath)
            });

        var manifest = await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, cancellationToken);
        dbManifest.Hierarchy.Single().FullPath = await fetchFullPath;

        // PK: Will this even happen? Should we log or even throw here?
        if (manifest == null)
            return FetchEntityResult<PresentationManifest>.Failure(
                "Unable to read and deserialize manifest from storage");

        manifest = manifest.SetGeneratedFields(dbManifest, pathGenerator,
            m => m.Hierarchy!.Single(h => h.Canonical));

        return FetchEntityResult<PresentationManifest>.Success(manifest);
    }
}

﻿using API.Converters;
using API.Features.Storage.Helpers;
using API.Infrastructure.AWS;
using MediatR;
using Models.API.Manifest;
using Repository;
using Repository.Helpers;

namespace API.Features.Manifest.Requests;

public class GetManifest(
    int customerId,
    string id,
    bool pathOnly,
    UrlRoots urlRoots) : IRequest<PresentationManifest?>
{
    public int CustomerId { get; } = customerId;
    public string Id { get; } = id;
    public bool PathOnly { get; } = pathOnly;
    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class GetManifestHandler(
    PresentationContext dbContext,
    IIIFS3Service iiifS3) : IRequestHandler<GetManifest, PresentationManifest?>
{
    #region Implementation of IRequestHandler<in GetManifest,PresentationManifest>

    public async Task<PresentationManifest?> Handle(GetManifest request, CancellationToken cancellationToken)
    {
        var dbManifest =
            await dbContext.RetrieveManifestAsync(request.CustomerId, request.Id, cancellationToken: cancellationToken);

        if (dbManifest == null) return null;

        if (request.PathOnly)
            return new()
            {
                FullPath = await ManifestRetrieval.RetrieveFullPathForManifest(dbManifest.Id, dbManifest.CustomerId,
                    dbContext, cancellationToken)
            };

        var manifest = await iiifS3.ReadIIIFFromS3<PresentationManifest>(dbManifest, cancellationToken);

        // PK: Will this even happen? Should we log or even throw here?
        if (manifest == null) return null;

        manifest = manifest.SetGeneratedFields(dbManifest, request.UrlRoots,
            m => m.Hierarchy!.Single(h => h.Canonical));
        
        return manifest;
    }

    #endregion
}
using API.Converters;
using API.Helpers;
using API.Infrastructure.AWS;
using MediatR;
using Models.API.Manifest;
using Models.Database.General;
using Repository;

namespace API.Features.Manifest.Requests;

public class GetManifestHierarchical(
    Hierarchy hierarchy,
    string slug,
    UrlRoots urlRoots) : IRequest<IIIF.Presentation.V3.Manifest?>
{
    public Hierarchy Hierarchy { get; } = hierarchy;
    public string Slug { get; } = slug;
    public UrlRoots UrlRoots { get; } = urlRoots;
}

public class GetManifestHierarchicalHandler(
    IMediator mediator,
    PresentationContext dbContext,
    IIIFS3Service iiifS3) : IRequestHandler<GetManifestHierarchical, IIIF.Presentation.V3.Manifest?>
{
    #region Implementation of IRequestHandler<in GetManifestHierarchical,PresentationManifest?>

    public async Task<IIIF.Presentation.V3.Manifest?> Handle(GetManifestHierarchical request,
        CancellationToken cancellationToken)
    {
        var flatId = request.Hierarchy.ManifestId ??
                     throw new InvalidOperationException(
                         "The differentiation of requests should prevent this from happening.");

        var manifest = await iiifS3.ReadIIIFFromS3<IIIF.Presentation.V3.Manifest>(
            BucketHelperX.GetManifestBucketKey(request.Hierarchy.CustomerId, flatId), cancellationToken);

        if (manifest == null)
            return null;

        manifest.Id = new UriBuilder(request.UrlRoots.BaseUrl!)
        {
            Path = $"{request.Hierarchy.CustomerId}/{request.Slug}"
        }.Uri.ToString();

        return manifest;
    }

    #endregion
}
using API.Converters;
using API.Converters.Streaming;
using AWS.Helpers;
using AWS.S3;
using AWS.Settings;
using Core.Streams;
using MediatR;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Repository.Paths;

namespace API.Features.Manifest.Requests;

public class GetManifestHierarchical(Hierarchy hierarchy) : IRequest<IIIF.Presentation.V3.Manifest?>
{
    public Hierarchy Hierarchy { get; } = hierarchy;
}

public class GetManifestHierarchicalHandler(
    IBucketReader bucketReader,
    PresentationContext dbContext,
    IPathGenerator pathGenerator,
    IOptions<AWSSettings> options) : IRequestHandler<GetManifestHierarchical, IIIF.Presentation.V3.Manifest?>
{
    private readonly AWSSettings settings = options.Value;
    
    public async Task<IIIF.Presentation.V3.Manifest?> Handle(GetManifestHierarchical request,
        CancellationToken cancellationToken)
    {
        var flatId = request.Hierarchy.ManifestId ??
                     throw new InvalidOperationException(
                         "The differentiation of requests should prevent this from happening.");
        
        if (!request.Hierarchy.Manifest!.LastProcessed.HasValue)
        {
            return null;
        }

        // So db can respond while we talk to S3
        var fetchFullPath =
            ManifestRetrieval.RetrieveFullPathForManifest(request.Hierarchy, dbContext, cancellationToken);
        
        var objectFromS3 = await bucketReader.GetObjectFromBucket(
            new(settings.S3.StorageBucket, BucketHelperX.GetManifestBucketKey(request.Hierarchy.CustomerId, flatId)),
            cancellationToken);

        if (objectFromS3.Stream.IsNull()) return null;

        request.Hierarchy.FullPath = await fetchFullPath;

        var hierarchicalId = pathGenerator.GenerateHierarchicalId(request.Hierarchy);

        return objectFromS3.GetDescriptionResourceWithId<IIIF.Presentation.V3.Manifest>(hierarchicalId);
    }
}

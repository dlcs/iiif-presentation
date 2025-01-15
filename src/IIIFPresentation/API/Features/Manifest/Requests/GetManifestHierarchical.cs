using API.Converters.Streaming;
using API.Helpers;
using AWS.Helpers;
using AWS.S3;
using AWS.Settings;
using Core.Streams;
using MediatR;
using Microsoft.Extensions.Options;
using Models.Database.General;
using Repository;
using Repository.Helpers;

namespace API.Features.Manifest.Requests;

public class GetManifestHierarchical(Hierarchy hierarchy) : IRequest<string?>
{
    public Hierarchy Hierarchy { get; } = hierarchy;
}

public class GetManifestHierarchicalHandler(
    IBucketReader bucketReader,
    PresentationContext dbContext,
    IPathGenerator pathGenerator,
    IOptions<AWSSettings> options) : IRequestHandler<GetManifestHierarchical, string?>
{
    private readonly AWSSettings settings = options.Value;
    
    public async Task<string?> Handle(GetManifestHierarchical request,
        CancellationToken cancellationToken)
    {          

        var flatId = request.Hierarchy.ManifestId ??
                     throw new InvalidOperationException(
                         "The differentiation of requests should prevent this from happening.");

        // So db can respond while we talk to S3
        var fetchFullPath =
            ManifestRetrieval.RetrieveFullPathForManifest(request.Hierarchy, dbContext, cancellationToken);
        
        var objectFromS3 = await bucketReader.GetObjectFromBucket(
            new(settings.S3.StorageBucket, BucketHelperX.GetManifestBucketKey(request.Hierarchy.CustomerId, flatId)),
            cancellationToken);

        if (objectFromS3.Stream.IsNull())
            return null;

        request.Hierarchy.FullPath = await fetchFullPath;

        var hierarchicalId = pathGenerator.GenerateHierarchicalId(request.Hierarchy);
        
        using var memoryStream = new MemoryStream();
        using var reader = new StreamReader(memoryStream);
        StreamingJsonProcessor.ProcessJson(objectFromS3.Stream, memoryStream,
            objectFromS3.Headers.ContentLength,
            new S3StoredJsonProcessor(hierarchicalId));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}

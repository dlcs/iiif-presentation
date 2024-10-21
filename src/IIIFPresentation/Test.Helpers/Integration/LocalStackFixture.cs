using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Test.Helpers.Integration;

/// <summary>
/// Xunit fixture that manages localstack and contains faked AWS clients for interactions.
/// </summary>
public class LocalStackFixture : IAsyncLifetime
{
    private readonly IContainer localStackContainer;
    private const int LocalStackContainerPort = 4566;
    
    // S3 Buckets
    public const string StorageBucketName = "presentation-storage";
        
    public Func<IAmazonS3> AWSS3ClientFactory { get; private set; }
    

    public LocalStackFixture()
    {
        // Configure container binding to host port 0, which will use a random free port
        var localStackBuilder = new ContainerBuilder()
            .WithImage("localstack/localstack")
            .WithCleanUp(true)
            .WithLabel("protagonist_test", "True")
            .WithEnvironment("DEFAULT_REGION", "eu-west-1")
            .WithEnvironment("SERVICES", "s3,sqs,sns")
            .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
            .WithEnvironment("DEBUG", "1")
            .WithPortBinding(0, LocalStackContainerPort);

        localStackContainer = localStackBuilder.Build();
    }

    public async Task InitializeAsync()
    {
        // Start local stack + create any required resources
        await localStackContainer.StartAsync();
        SetAWSClientFactories();
        await SeedAwsResources();
    }

    public Task DisposeAsync() => localStackContainer.StopAsync();

    private void SetAWSClientFactories()
    {
        // Get the actual port number used as we bound to 0
        var localStackPort = localStackContainer.GetMappedPublicPort(LocalStackContainerPort);
        
        // LocalStack url
        var localStackUrl = $"http://localhost:{localStackPort}/";
        
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.EUWest1,
            UseHttp = true,
            ForcePathStyle = true,
            ServiceURL = localStackUrl
        };

        AWSS3ClientFactory = () => new AmazonS3Client(new BasicAWSCredentials("foo", "bar"), s3Config);
    }
    
    private async Task SeedAwsResources()
    {
        // Create basic buckets used by DLCS
        var amazonS3Client = AWSS3ClientFactory();
        await amazonS3Client.PutBucketAsync(StorageBucketName);
        await amazonS3Client.PutObjectAsync(new PutObjectRequest()
        {
            BucketName = StorageBucketName,
            Key = "1/collections/IiifCollection", 
            ContentBody = $$"""
                            {
                            "type": "Collection",
                            "behavior": [
                                "public-iiif"
                            ],
                            "label": {
                                "en": [
                                    "first child - iiif"
                                ]
                            }
                            }
                            """
        });
    }
}
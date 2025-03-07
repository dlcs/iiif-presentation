using System.Net;
using Amazon.S3;
using API.Infrastructure.Helpers;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Response;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.Manifest;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(CollectionDefinitions.StorageCollection.CollectionName)]
public class DeleteManifestTests : IClassFixture<PresentationAppFactory<Program>>
{
    private readonly HttpClient httpClient;
    private readonly PresentationContext dbContext;
    private readonly IAmazonS3 amazonS3;
    private readonly IETagManager etagManager;
    private const int Customer = 1;

    public DeleteManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture));

        etagManager = (IETagManager) factory.Services.GetRequiredService(typeof(IETagManager));

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task DeleteManifest_DeletesManifest()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/manifests/{dbManifest.Id}");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await dbContext.Manifests.CountAsync(m => m.Id == dbManifest.Id)).Should().Be(0, "the manifest was deleted");
    }

    [Fact]
    public async Task DeleteManifest_NotFound_WhenDoesNotExists()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
                $"{Customer}/manifests/this_does_not_exist_1610");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteManifest_Forbidden_WhenNoAuthOrExtras()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();

        var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{Customer}/manifests/{dbManifest.Id}");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Cleanup
        dbContext.Manifests.Remove(dbManifest);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task DeleteManifest_DeletesInS3()
    {
        // Arrange
        var slug = nameof(DeleteManifest_DeletesInS3);
        var manifest = new PresentationManifest
        {
            Parent = $"http://localhost/{Customer}/collections/{RootCollection.Id}",
            Slug = slug
        };

        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Post, $"{Customer}/manifests", manifest.AsJson());

        var response = await httpClient.AsCustomer().SendAsync(requestMessage);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseCollection = await response.ReadAsPresentationResponseAsync<PresentationManifest>();
        var id = responseCollection!.Id.GetLastPathElement();

        requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/manifests/{id}");


        // Act
        response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await new Func<Task>(async () => await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"{Customer}/manifests/{id}")).Should().ThrowAsync<AmazonS3Exception>();
    }
}

using System.Net;
using Amazon.S3;
using API.Tests.Integration.Infrastructure;
using Core.Helpers;
using Core.Response;
using DLCS.API;
using DLCS.Exceptions;
using DLCS.Models;
using FakeItEasy;
using IIIF.Serialisation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Models.API.General;
using Models.API.Manifest;
using Models.Database.General;
using Models.DLCS;
using Repository;
using Services.TextServices;
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
    private static readonly ITextBuilderClient TextServicesClient = A.Fake<ITextBuilderClient>();
    private static readonly IDlcsApiClient DlcsApiClient = A.Fake<IDlcsApiClient>();
    private const int Customer = 1;

    public DeleteManifestTests(StorageFixture storageFixture, PresentationAppFactory<Program> factory)
    {
        dbContext = storageFixture.DbFixture.DbContext;
        amazonS3 = storageFixture.LocalStackFixture.AWSS3ClientFactory();
        A.CallTo(() => TextServicesClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._)).Returns(true);

        httpClient = factory.ConfigureBasicIntegrationTestHttpClient(storageFixture.DbFixture,
            appFactory => appFactory.WithLocalStack(storageFixture.LocalStackFixture),
            services => services
                .AddSingleton(TextServicesClient)
                .AddSingleton(DlcsApiClient));

        storageFixture.DbFixture.CleanUp();
    }

    [Fact]
    public async Task DeleteManifest_DeletesManifest()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await dbContext.Manifests.CountAsync(m => m.Id == dbManifest.Id)).Should().Be(0, "the manifest was deleted");
    }

    [Fact]
    public async Task DeleteManifest_DeletesTextBuilderJob_WhenManifestHasPipelineJob()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest().WithTestPipelineJob()).Entity;
        await dbContext.SaveChangesAsync();

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => TextServicesClient.DeleteJob(
            A<TextJobId>.That.Matches(j => j.CustomerId == Customer && j.ResourceId == dbManifest.Id),
            A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task DeleteManifest_DoesNotCallTextBuilder_WhenManifestHasNoPipelineJob()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.SaveChangesAsync();

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => TextServicesClient.DeleteJob(
            A<TextJobId>.That.Matches(j => j.ResourceId == dbManifest.Id),
            A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteManifest_DeletesManifest_WhenTextBuilderClientThrows()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest().WithTestPipelineJob()).Entity;
        await dbContext.SaveChangesAsync();
        A.CallTo(() => TextServicesClient.DeleteJob(A<TextJobId>._, A<CancellationToken>._))
            .Throws(new HttpRequestException("text-services unreachable"));

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await dbContext.Manifests.CountAsync(m => m.Id == dbManifest.Id)).Should().Be(0,
            "the manifest should still be deleted even though text-services was unreachable");
    }

    [Fact]
    public async Task DeleteManifest_RemovesManifestFromAssets_WhenManifestHasAssets()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        var firstAsset = new AssetId(Customer, 1, $"{nameof(DeleteManifest_RemovesManifestFromAssets_WhenManifestHasAssets)}_1");
        var secondAsset = new AssetId(Customer, 2, $"{nameof(DeleteManifest_RemovesManifestFromAssets_WhenManifestHasAssets)}_2");
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, assetId: firstAsset);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest, assetId: secondAsset);
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest);
        await dbContext.SaveChangesAsync();

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DlcsApiClient.UpdateAssetManifest(Customer,
            A<ICollection<string>>.That.Matches(a =>
                a.Count == 2 && a.Contains(firstAsset.ToString()) && a.Contains(secondAsset.ToString())),
            OperationType.Remove,
            A<List<string>>.That.Matches(m => m.Count == 1 && m[0] == dbManifest.Id),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteManifest_DoesNotCallDlcs_WhenManifestHasNoAssets()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest);
        await dbContext.SaveChangesAsync();

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        A.CallTo(() => DlcsApiClient.UpdateAssetManifest(A<int>._, A<ICollection<string>>._, A<OperationType>._,
            A<List<string>>.That.Matches(m => m.Contains(dbManifest.Id)), A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteManifest_DeletesManifest_WhenRemovingManifestFromAssetsFails()
    {
        // Arrange
        var dbManifest = (await dbContext.Manifests.AddTestManifest()).Entity;
        await dbContext.CanvasPaintings.AddTestCanvasPainting(dbManifest,
            assetId: new AssetId(Customer, 1, nameof(DeleteManifest_DeletesManifest_WhenRemovingManifestFromAssetsFails)));
        await dbContext.SaveChangesAsync();
        A.CallTo(() => DlcsApiClient.UpdateAssetManifest(A<int>._, A<ICollection<string>>._, A<OperationType>._,
                A<List<string>>.That.Matches(m => m.Contains(dbManifest.Id)), A<CancellationToken>._))
            .Throws(new DlcsException("DLCS unreachable", HttpStatusCode.InternalServerError));

        var requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
            $"{Customer}/manifests/{dbManifest.Id}", dbContext.GetETag(dbManifest));

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await dbContext.Manifests.CountAsync(m => m.Id == dbManifest.Id)).Should().Be(0,
            "the manifest should still be deleted even though the DLCS asset update failed");
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
    public async Task DeleteManifest_FailsToDeleteManifest_WhenEtagDoesNotMatch()
    {
        // Arrange
        var requestMessage =
            HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete,
                $"{Customer}/manifests/FirstChildManifest");

        // Act
        var response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        
        var errorResponse = await response.ReadAsPresentationResponseAsync<Error>();
        errorResponse!.ErrorTypeUri.Should().Be("http://localhost/errors/DeleteResourceErrorType/EtagNotMatching");
        errorResponse.Detail.Should().Be("Etag does not match");
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

        requestMessage = HttpRequestMessageBuilder.GetPrivateRequest(HttpMethod.Delete, $"{Customer}/manifests/{id}",
            dbContext.GetETag(id, Customer, ResourceType.IIIFManifest));


        // Act
        response = await httpClient.AsCustomer().SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await new Func<Task>(async () => await amazonS3.GetObjectAsync(LocalStackFixture.StorageBucketName,
            $"{Customer}/manifests/{id}")).Should().ThrowAsync<AmazonS3Exception>();
    }
}

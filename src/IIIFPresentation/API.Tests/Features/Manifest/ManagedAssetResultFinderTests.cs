using API.Features.Manifest;
using API.Settings;
using API.Tests.Integration.Infrastructure;
using AWS.Settings;
using Core.Exceptions;
using DLCS;
using DLCS.API;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManagedAssetResultFinderTests
{
    private readonly ManagedAssetResultFinder sut;
    private const int DefaultCustomer = 1;
    private const int DefaultSpace = 2;
    private readonly PresentationContext dbContext;
    private readonly IDlcsApiClient dlcsApiClient;
    
    public ManagedAssetResultFinderTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        
        dlcsApiClient = A.Fake<IDlcsApiClient>();
        
        var options = Options.Create(new ApiSettings()
        {
            AWS = new AWSSettings(),
            DLCS = new DlcsSettings
            {
                ApiUri = new Uri("https://localhost")
            }
        });
        
        sut = new ManagedAssetResultFinder(dlcsApiClient, dbContext, options,
            new NullLogger<ManagedAssetResultFinder>());
    }

    [Fact]
    public async Task CheckAssetsFromItemsExist_ThrowsError_IfNoAssetFoundInDlcs()
    {
        // Arrange
        var assetsToCheck = new List<AssetId>
        {
            new (DefaultCustomer, DefaultSpace, "doesNotExist"),
            new (DefaultCustomer, DefaultSpace, "doesNotExist2")
        };
        
        // Act
        Func<Task> action = () => sut.CheckAssetsFromItemsExist(assetsToCheck, DefaultCustomer, [], CancellationToken.None);
        
        // Assert
        await action.Should().ThrowAsync<PresentationException>()
            .WithMessage($"Suspected DLCS assets from items not found: (assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist), (assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist2)");
    }
    
    [Fact]
    public async Task CheckAssetsFromItemsExist_NothingToUpdate_IfAllAssetsInExistingManifest()
    {
        var assetId = "inExisting";
        
        // Arrange
        var assetsToCheck = new List<AssetId>
        {
            new (DefaultCustomer, DefaultSpace, assetId)
        };

        List<AssetId> existingAssets = [new(DefaultCustomer, DefaultSpace, assetId)];
        
        // Act
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(assetsToCheck, DefaultCustomer, existingAssets,
            CancellationToken.None);
        
        // Assert
        assetsToUpdate.Should().BeEmpty();
    }
    
    [Fact]
    public async Task CheckAssetsFromItemsExist_AssetToUpdate_IfAllAssetsInAnotherManifest()
    {
        var assetId = "inAnother";
        
        // Arrange
        var assetsToCheck = new List<AssetId>
        {
            new (DefaultCustomer, DefaultSpace, assetId)
        };

        await dbContext.Manifests.AddTestManifest(canvasPaintings:
        [
            new CanvasPainting
            {
                AssetId = new AssetId(DefaultCustomer, DefaultSpace, assetId)
            }
        ]);
        await dbContext.SaveChangesAsync();
        
        // Act
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(assetsToCheck, DefaultCustomer, [],
            CancellationToken.None);
        
        // Assert
        assetsToUpdate.Single().Asset.Should().Be(assetId);
    }
    
    [Fact]
    public async Task CheckAssetsFromItemsExist_AssetToUpdate_IfAssetsFromMixtureOfSources()
    {
        var inAnotherManifest = "inAnother";
        var inExistingManifest = "inExisting";
        var inDlcs = "inDlcs";
        
        // Arrange
        var assetsToCheck = new List<AssetId>
        {
            new (DefaultCustomer, DefaultSpace, inAnotherManifest),
            new (DefaultCustomer, DefaultSpace, inExistingManifest),
            new (DefaultCustomer, DefaultSpace, inDlcs)
        };

        await dbContext.Manifests.AddTestManifest(canvasPaintings:
        [
            new CanvasPainting
            {
                AssetId = new AssetId(DefaultCustomer, DefaultSpace, inAnotherManifest)
            }
        ]);
        await dbContext.SaveChangesAsync();
        
        List<AssetId> existingAssets = [new(DefaultCustomer, DefaultSpace, inExistingManifest)];
        
        A.CallTo(() => dlcsApiClient.GetCustomerImages(A<int>._, A<ICollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            [
                JObject.Parse($$"""
                                {
                                    "id": "{{inDlcs}}",
                                    "space": {{DefaultSpace}}
                                }
                                """
                )
            ]);
        
        // Act
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(assetsToCheck, DefaultCustomer, existingAssets,
            CancellationToken.None);
        
        // Assert
        assetsToUpdate.Should().HaveCount(2);
        assetsToUpdate.First().Asset.Should().Be(inAnotherManifest);
        assetsToUpdate.Last().Asset.Should().Be(inDlcs);
    }
}

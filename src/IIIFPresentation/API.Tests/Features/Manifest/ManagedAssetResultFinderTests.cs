using API.Features.Manifest;
using API.Tests.Integration.Infrastructure;
using Core.Exceptions;
using DLCS.API;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Database;
using Models.DLCS;
using Newtonsoft.Json.Linq;
using Repository;
using Services.Manifests.Model;
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
        
        sut = new ManagedAssetResultFinder(dlcsApiClient, dbContext,
            new NullLogger<ManagedAssetResultFinder>());
    }

    [Fact]
    public async Task CheckAssetsFromItemsExist_ThrowsError_IfNoAssetFoundInDlcs()
    {
        // Arrange
        var canvasPaintings = new List<InterimCanvasPainting>
        {
            new ()
            {
                SuspectedAssetId = "doesNotExist",
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/doesNotExist")
            },
            new ()
            {
                SuspectedAssetId = "doesNotExist2",
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/doesNotExist2")
            }
        };
        
        // Act
        Func<Task> action = () => sut.CheckAssetsFromItemsExist(canvasPaintings, DefaultCustomer, [], CancellationToken.None);
        
        // Assert
        await action.Should().ThrowAsync<PresentationException>()
            .WithMessage($"Suspected DLCS assets from items not found: (id: {canvasPaintings[0].CanvasOriginalId}, assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist), (id: {canvasPaintings[1].CanvasOriginalId}, assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist2)");
    }
    
    [Fact]
    public async Task CheckAssetsFromItemsExist_NothingToUpdate_IfAllAssetsInExistingManifest()
    {
        var assetId = "inExisting";
        
        // Arrange
        var canvasPaintings = new List<InterimCanvasPainting>
        {
            new ()
            {
                SuspectedAssetId = assetId,
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/{assetId}")
            }
        };

        List<AssetId> existingAssets = [new(DefaultCustomer, DefaultSpace, assetId)];
        
        // Act
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(canvasPaintings, DefaultCustomer, existingAssets,
            CancellationToken.None);
        
        // Assert
        assetsToUpdate.Should().BeEmpty();
    }
    
    [Fact]
    public async Task CheckAssetsFromItemsExist_AssetToUpdate_IfAllAssetsInAnotherManifest()
    {
        var assetId = "inAnother";
        
        // Arrange
        var canvasPaintings = new List<InterimCanvasPainting>
        {
            new ()
            {
                SuspectedAssetId = assetId,
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/{assetId}")
            }
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
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(canvasPaintings, DefaultCustomer, [],
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
        var canvasPaintings = new List<InterimCanvasPainting>
        {
            new ()
            {
                SuspectedAssetId = inAnotherManifest,
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/{inAnotherManifest}")
            },
            new ()
            {
                SuspectedAssetId = inExistingManifest,
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/{inExistingManifest}")
            },
            new ()
            {
                SuspectedAssetId = inDlcs,
                SuspectedSpace = DefaultSpace,
                CustomerId = DefaultCustomer,
                CanvasOriginalId = new Uri($"https://dlcs.api/{DefaultCustomer}/{DefaultSpace}/{inDlcs}")
            }
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
        var assetsToUpdate = await sut.CheckAssetsFromItemsExist(canvasPaintings, DefaultCustomer, existingAssets,
            CancellationToken.None);
        
        // Assert
        assetsToUpdate.Should().HaveCount(2);
        assetsToUpdate.First().Asset.Should().Be(inAnotherManifest);
        assetsToUpdate.Last().Asset.Should().Be(inDlcs);
    }
}

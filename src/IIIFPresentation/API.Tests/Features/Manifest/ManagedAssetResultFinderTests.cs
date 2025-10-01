using API.Features.Manifest;
using API.Tests.Integration.Infrastructure;
using Core.Exceptions;
using DLCS.API;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Manifests.Model;
using Test.Helpers.Integration;

namespace API.Tests.Features.Manifest;

[Trait("Category", "Database")]
[Collection(CollectionDefinitions.DatabaseCollection.CollectionName)]
public class ManagedAssetResultFinderTests
{
    private readonly ManagedAssetResultFinder sut;
    private const int DefaultCustomer = 1;
    private const int DefaultSpace = 2;
    
    public ManagedAssetResultFinderTests(PresentationContextFixture dbFixture)
    {
        var presentationContext = dbFixture.DbContext;
        
        var dlcsClient = A.Fake<IDlcsApiClient>();
        
        sut = new ManagedAssetResultFinder(dlcsClient, presentationContext,
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
        Func<Task> action = () => sut.CheckAssetsFromItemsExist(canvasPaintings, DefaultCustomer, CancellationToken.None);
        
        // Assert
        await action.Should().ThrowAsync<PresentationException>()
            .WithMessage($"Suspected DLCS assets from items not found: (id: {canvasPaintings[0].CanvasOriginalId}, assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist), (id: {canvasPaintings[1].CanvasOriginalId}, assetId: {DefaultCustomer}/{DefaultSpace}/doesNotExist2)");
    }
}

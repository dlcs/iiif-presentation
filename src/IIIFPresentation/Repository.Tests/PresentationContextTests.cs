using Microsoft.EntityFrameworkCore;
using Models.DLCS;
using Test.Helpers.Helpers;
using Test.Helpers.Integration;

namespace Repository.Tests;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class PresentationContextTests
{
    private readonly PresentationContext dbContext;
    
    public PresentationContextTests(PresentationContextFixture dbFixture)
    {
        dbContext = dbFixture.GetNewPresentationContext();;
        
        dbContext.SaveChanges();
    }

    [Fact]
    public async Task CanvasPaintings_AssetIdIndex_EnsuresUnique()
    {
        // Arrange
        var testManifest = await dbContext.Manifests.AddTestManifest();

        var assetId = AssetId.FromString("1/2/stuff");
        
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            assetId: assetId, canvasOrder: 0, choiceOrder: 0);
        
        // Act
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            assetId: assetId, canvasOrder: 0, choiceOrder: 0);
        
        Action action = () => dbContext.SaveChanges();

        // assert
        action.Should().Throw<DbUpdateException>();
    }
    
    [Fact]
    public async Task CanvasPaintings_CanvasOriginalIndex_EnsuresUnique()
    {
        // Arrange
        var testManifest = await dbContext.Manifests.AddTestManifest();
        
        var canvasOriginalId = new Uri("https://stuff.com");
        
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 0);
        
        // Act
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 0);
        
        Action action = () => dbContext.SaveChanges();

        // assert
        action.Should().Throw<DbUpdateException>();
    }
    
    [Fact]
    public async Task CanvasPaintings_AssetIdAndCanvasOriginalIdIndex_EnsuresUnique()
    {
        // Arrange
        var testManifest = await dbContext.Manifests.AddTestManifest();
        
        var assetId = AssetId.FromString("1/2/stuff");
        var canvasOriginalId = new Uri("https://stuff.com");
        
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            assetId: assetId, canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 0);
        
        // Act
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            assetId: assetId, canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 0);
        
        Action action = () => dbContext.SaveChanges();

        // assert
        action.Should().Throw<DbUpdateException>();
    }
    
    [Fact]
    public async Task CanvasPaintings_AssetIdToCanvasOriginalIdIndex_EnsuresUnique()
    {
        // Arrange
        var testManifest = await dbContext.Manifests.AddTestManifest();
        
        var assetId = AssetId.FromString("1/2/stuff");
        var canvasOriginalId = new Uri("https://stuff.com");
        
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            assetId: assetId, canvasOrder: 0, choiceOrder: 0);
        
        // Act
        await dbContext.CanvasPaintings.AddTestCanvasPainting(testManifest.Entity, id: "first",
            canvasOriginalId: canvasOriginalId, canvasOrder: 0, choiceOrder: 0);
        
        Action action = () => dbContext.SaveChanges();

        // assert
        action.Should().Throw<DbUpdateException>();
    }
}

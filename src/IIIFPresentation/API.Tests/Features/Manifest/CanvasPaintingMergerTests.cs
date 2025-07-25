using API.Features.Manifest;
using Models.Database;
using Test.Helpers;
using Test.Helpers.Helpers;

namespace API.Tests.Features.Manifest;

public class CanvasPaintingMergerTests
{
    private CanvasPaintingMerger sut = new();

    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenTwoEmptyLists()
    {
        // Arrange and act
        var merged = sut.CombinePaintedResources([], [], []);

        // Assert
        merged.Should().BeEmpty();
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenOnlyPaintedResources()
    {
        // Arrange
        var id = "paintedResource";
        
        var canvasPaintings = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{id}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{id}_2", cp => cp.WithCanvasChoiceOrder(1, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources([], canvasPaintings, []);

        // Assert
        merged.First().Id.Should().Be($"{id}_1");
        merged.Last().Id.Should().Be($"{id}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenOnlyPaintedResourcesNeedReordering()
    {
        // Arrange
        var id = "paintedResource";
        
        var canvasPaintings = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{id}_2", cp => cp.WithCanvasChoiceOrder(1, 0))
            .WithCanvasPainting($"{id}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{id}_3", cp => cp.WithCanvasChoiceOrder(0, 1)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources([], canvasPaintings, []);

        // Assert
        merged.First().Id.Should().Be($"{id}_1");
        merged.Last().Id.Should().Be($"{id}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenOnlyItems()
    {
        // Arrange
        var id = "items";
        
        var canvasPaintings = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{id}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{id}_2", cp => cp.WithCanvasChoiceOrder(1, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintings, [], []);

        // Assert
        merged.First().Id.Should().Be($"{id}_1");
        merged.Last().Id.Should().Be($"{id}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenOrderedItemsThenPaintedResources()
    {
        // Arrange
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{itemsId}_2", cp => cp.WithCanvasChoiceOrder(1, 0)).Build();
        
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp => cp.WithCanvasChoiceOrder(2, 0))
            .WithCanvasPainting($"{paintedResourceId}_2", cp => cp.WithCanvasChoiceOrder(3, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{itemsId}_1");
        merged[1].Id.Should().Be($"{itemsId}_2");
        merged[2].Id.Should().Be($"{paintedResourceId}_1");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenOrderedPaintedResourcesThenItems()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{paintedResourceId}_2", cp => cp.WithCanvasChoiceOrder(1, 0)).Build();
        
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp => cp.WithCanvasChoiceOrder(2, 0))
            .WithCanvasPainting($"{itemsId}_2", cp => cp.WithCanvasChoiceOrder(3, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{paintedResourceId}_1");
        merged[1].Id.Should().Be($"{paintedResourceId}_2");
        merged[2].Id.Should().Be($"{itemsId}_1");
        merged.Last().Id.Should().Be($"{itemsId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenMixingPaintedResourcesAndItems()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{paintedResourceId}_2", cp => cp.WithCanvasChoiceOrder(2, 0)).Build();
        
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp => cp.WithCanvasChoiceOrder(1, 0))
            .WithCanvasPainting($"{itemsId}_2", cp => cp.WithCanvasChoiceOrder(3, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{paintedResourceId}_1");
        merged[1].Id.Should().Be($"{itemsId}_1");
        merged[2].Id.Should().Be($"{paintedResourceId}_2");
        merged.Last().Id.Should().Be($"{itemsId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenMixingItemsAndPaintedResources()
    {
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{itemsId}_2", cp => cp.WithCanvasChoiceOrder(2, 0)).Build();
        
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp => cp.WithCanvasChoiceOrder(1, 0))
            .WithCanvasPainting($"{paintedResourceId}_2", cp => cp.WithCanvasChoiceOrder(3, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{itemsId}_1");
        merged[1].Id.Should().Be($"{paintedResourceId}_1");
        merged[2].Id.Should().Be($"{itemsId}_2");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenItemsTrackedByPaintedResources()
    {
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemsId}_1");
            })
            .WithCanvasPainting($"{itemsId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemsId}_2");
            }).Build();
        
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemsId}_1");
            })
            .WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemsId}_2");
            }).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{paintedResourceId}_1");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
    }
}

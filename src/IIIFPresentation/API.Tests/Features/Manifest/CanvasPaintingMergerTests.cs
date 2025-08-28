﻿using API.Features.Manifest;
using API.Features.Manifest.Exceptions;
using Core.Web;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Test.Helpers.Helpers;

namespace API.Tests.Features.Manifest;

public class CanvasPaintingMergerTests
{
    private CanvasPaintingMerger sut = new(new PathRewriteParser(Options.Create(PathRewriteOptions.Default),
        new NullLogger<PathRewriteParser>()));

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
    public void CombinePaintedResources_MergesCorrectly_WhenOrderedItemsThenPaintedResourcesWithGaps()
    {
        // Arrange
        var itemsId = "items";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemsId}_1", cp => cp.WithCanvasChoiceOrder(0, 0))
            .WithCanvasPainting($"{itemsId}_2", cp => cp.WithCanvasChoiceOrder(1, 0)).Build();
        
        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp => cp.WithCanvasChoiceOrder(20, 0))
            .WithCanvasPainting($"{paintedResourceId}_2", cp => cp.WithCanvasChoiceOrder(30, 0)).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{itemsId}_1");
        merged[1].Id.Should().Be($"{itemsId}_2");
        merged[2].Id.Should().Be($"{paintedResourceId}_1");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
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
        // Arange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            })
            .WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_2");
            }).Build();

        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
            })
            .WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
            }).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{paintedResourceId}_1");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenItemsTrackedByPaintedResourcesWithMultipleSameCanvasOriginalId()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            })
            .WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
            })
            .WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
            }).Build();
        
        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        merged.First().Id.Should().Be($"{paintedResourceId}_1");
        merged.Last().Id.Should().Be($"{paintedResourceId}_2");
    }
    
    [Fact]
    public void CombinePaintedResources_MergesCorrectly_WhenItemsTrackedByPaintedResourcesWithoutSettingCanvasOrder()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1").Build();

        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [new Canvas()
        {
            Id = $"https://localhost/1/{paintedResourceId}_1",
        }]);
        

        // Assert
        var canvasPainting = merged.Single();

        canvasPainting.Id.Should().Be($"{paintedResourceId}_1");
    }

    [Fact]
    public void CombinePaintedResources_UpdatesCanvasLabel_WhenItemsTrackedByPaintedResources()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
                cp.CanvasLabel = new LanguageMap("canvas", "label");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1").Build();

        // Act
        var merged = sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [
            new Canvas()
            {
                Id = $"https://localhost/1/{paintedResourceId}_1",
            }
        ]);


        // Assert
        var canvasPainting = merged.Single();

        canvasPainting.Id.Should().Be($"{paintedResourceId}_1");
        canvasPainting.CanvasLabel.First().Key.Should().Be("canvas");
    }

    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithPaintingAnnotation()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
            }).Build();

        var canvas = ManifestTestCreator.Canvas($"https://localhost/1/{paintedResourceId}_1").WithImage().Build();
        
        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [canvas]);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage($"canvas painting with id {paintedResourceId}_1 cannot contain an annotation body");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithMismatchedCanvasLabel()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
                cp.CanvasLabel = new LanguageMap("mismatch", "mismatch_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasLabel = new LanguageMap("mismatch", "mismatch_2");
            }).Build();

        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [new Canvas()
        {
            Id = $"https://localhost/1/{paintedResourceId}_1",
        }]);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage($"canvas painting with id {paintedResourceId}_1 does not have a matching canvas label");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithMismatchedLabel()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
                cp.Label = new LanguageMap("mismatch", "mismatch_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.Label = new LanguageMap("mismatch", "mismatch_2");
            }).Build();

        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [new Canvas()
        {
            Id = $"https://localhost/1/{paintedResourceId}_1",
        }]);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage($"canvas painting with id {paintedResourceId}_1 does not have a matching label");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithMismatchedCanvasOrder()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
            }).Build();

        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [new Canvas()
        {
            Id = $"https://localhost/1/{paintedResourceId}_1",
        }]);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage($"canvas painting with id {paintedResourceId}_1 does not have a matching canvas order");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithMismatchedChoiceOrder()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 1;
            }).Build();

        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, [new Canvas()
        {
            Id = $"https://localhost/1/{paintedResourceId}_1",
        }]);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage($"canvas painting with id {paintedResourceId}_1 does not have a matching choice order");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenItemsTrackedByPaintedResourcesWithMultipleSameCanvasOriginalIdWithoutFindingAMatchingCanvasOrder()
    {
        // Arrange
        var paintedResourceId = "paintedResource";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{paintedResourceId}_1");
            }).Build();
        
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
            }).WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 2;
                cp.ChoiceOrder = 0;
            }).Build();
        
        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage(
                $"Canvas with id https://localhost/1/{paintedResourceId}_1 refers to multiple canvases, and the matching canvas order cannot be found");
    }
    
    [Fact]
    public void CombinePaintedResources_ThrowsError_WhenUnmatchedItemMatchesOrderOfCanvasPainting()
    {
        // Arrange
        var itemId = "item";
        var canvasPaintingItems = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{itemId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemId}_1");
            }).WithCanvasPainting($"{itemId}_2", cp =>
            {
                cp.CanvasOrder = 1;
                cp.ChoiceOrder = 0;
                cp.CanvasOriginalId = new Uri($"https://localhost/1/{itemId}_2");
            }).Build();

        var paintedResourceId = "paintedResource";
        var canvasPaintingPaintedResources = ManifestTestCreator.CanvasPaintings()
            .WithCanvasPainting($"{paintedResourceId}_1", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
            }).WithCanvasPainting($"{paintedResourceId}_2", cp =>
            {
                cp.CanvasOrder = 0;
                cp.ChoiceOrder = 0;
            }).Build();
        
        // Act
        Action action = () => sut.CombinePaintedResources(canvasPaintingItems, canvasPaintingPaintedResources, []);

        // Assert
        action.Should().ThrowExactly<CanvasPaintingMergerException>()
            .WithMessage(
                $"Canvas painting records with the following id's conflict with the order from items - {paintedResourceId}_1,{paintedResourceId}_2");
    }
}

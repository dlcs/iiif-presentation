using API.Helpers;
using Models.API.Manifest;
using Newtonsoft.Json.Linq;

namespace API.Tests.Helpers;

public class PaintedResourceXTest
{
    [Fact]
    public void HasAsset_False_NullPaintedResources()
    {
        List<PaintedResource>? paintedResources = null;
        paintedResources.HasAsset().Should().BeFalse();
    }
    
    [Fact]
    public void HasAsset_False_EmptyPaintedResources()
    {
        var paintedResources = new List<PaintedResource>();
        paintedResources.HasAsset().Should().BeFalse();
    }
    
    [Fact]
    public void HasAsset_True_WhenAssetPresent()
    {
        // Arrange
        dynamic asset = new JObject();
        asset.someObject = "someValue";
        
        var paintedResources = new List<PaintedResource>()
        {
            new ()
            {
                Asset = asset,
                CanvasPainting = new CanvasPainting
                {
                    CanvasId = ""
                }
            }
        };

        // Act
        var hasAsset = paintedResources.HasAsset();

        // Assert
        hasAsset.Should().BeTrue();
    }
    
    [Fact]
    public void HasAsset_False_WhenNoAssetPresent()
    {
        // Arrange
        var paintedResources = new List<PaintedResource>()
        {
            new ()
            {
                CanvasPainting = new CanvasPainting
                {
                    CanvasId = ""
                }
            }
        };

        // Act
        var hasAsset = paintedResources.HasAsset();

        // Assert
        hasAsset.Should().BeFalse();
    }

    [Fact]
    public void GetRequiredNumberOfCanvases_0_IfPaintedResourcesNull()
    {
        List<PaintedResource>? paintedResources = null;
        paintedResources.GetRequiredNumberOfCanvases().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_0_IfPaintedResourcesEmpty()
    {
        var paintedResources = new List<PaintedResource>();
        paintedResources.GetRequiredNumberOfCanvases().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfAllPaintedResourcesNullCanvasOrder()
    {
        List<PaintedResource> paintedResources = [
            new() { CanvasPainting = new CanvasPainting { CanvasId = "1" } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "2" } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "3" } },
        ];
        paintedResources.GetRequiredNumberOfCanvases().Should().Be(3);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfAllHaveCanvasOrder()
    {
        List<PaintedResource> paintedResources = [
            new() { CanvasPainting = new CanvasPainting { CanvasId = "1", CanvasOrder = 1} },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "2", CanvasOrder = 1 } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "3", CanvasOrder = 2 } },
        ];
        paintedResources.GetRequiredNumberOfCanvases().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfAllMixedCanvasOrderAndNot()
    {
        List<PaintedResource> paintedResources = [
            new() { CanvasPainting = new CanvasPainting { CanvasId = "1", CanvasOrder = 1} },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "2" } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "3", CanvasOrder = 2 } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "4" } },
            new() { CanvasPainting = new CanvasPainting { CanvasId = "5", CanvasOrder = 1 } },
            new() { CanvasPainting = null, Asset = new JObject() },
        ];
        paintedResources.GetRequiredNumberOfCanvases().Should().Be(5);
    }
}

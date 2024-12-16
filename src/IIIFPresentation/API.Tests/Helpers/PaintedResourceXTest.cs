using API.Helpers;
using Models.API.Manifest;
using Newtonsoft.Json.Linq;

namespace API.Tests.Helpers;

public class PaintedResourceXTest
{
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
}

using API.Helpers;
using Models.Database;

namespace API.Tests.Helpers;

public class CanvasPaintingXTests
{
    [Fact]
    public void GetRequiredNumberOfCanvases_0_IfPaintedResourcesNull()
    {
        List<CanvasPainting>? canvasPaintings = null;
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_0_IfPaintedResourcesEmpty()
    {
        var canvasPaintings = new List<CanvasPainting>();
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfSingleNullCanvas()
    {
        List<CanvasPainting> canvasPaintings = [new()];
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(1);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfMultipleNullCanvas()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 2 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfAllHaveCanvasOrderWithMultipleSameCanvas()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 2 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IfAllMixedCanvasOrderAndNot()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new(),
            new() { CanvasOrder = 2 },
            new(),
            new() { CanvasOrder = 1 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(3);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvases_Correct_IgnoresItemsWithId()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new(),
            new() { CanvasOrder = 2 },
            new(),
            new() { CanvasOrder = 1 },
            new() { Id = "one" },
            new() { Id = "one", CanvasOrder = 1 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvases().Should().Be(3);
    }
}

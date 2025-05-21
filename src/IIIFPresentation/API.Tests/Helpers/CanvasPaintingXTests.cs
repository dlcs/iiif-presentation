using API.Helpers;
using Models.Database;

namespace API.Tests.Helpers;

public class CanvasPaintingXTests
{
    [Fact]
    public void GetRequiredNumberOfCanvasIds_0_IfPaintedResourcesNull()
    {
        List<CanvasPainting>? canvasPaintings = null;
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_0_IfPaintedResourcesEmpty()
    {
        var canvasPaintings = new List<CanvasPainting>();
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IfSingleCanvas()
    {
        List<CanvasPainting> canvasPaintings = [new()];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(1);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_AllHaveId()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { Id = "1" },
            new() { Id = "2" },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MultipleCanvasOrder()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 2 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MultipleCanvasOriginalId()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/2") },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IfAllHaveCanvasOrderWithMultipleSame()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 2 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IfAllHaveCanvasOriginalIdWithMultipleSame()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/2") },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MixedCanvasOrderAndNot()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new(),
            new() { CanvasOrder = 2 },
            new(),
            new() { CanvasOrder = 1 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(3);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MixedCanvasOriginalIdAndNot()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
            new(),
            new() { CanvasOriginalId = new Uri("https://canvas.ex/2") },
            new(),
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(3);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IgnoresItemsWithId_MixedCanvasOriginalIdAndCanvasOrder()
    {
        List<CanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1"), CanvasOrder = 1 },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1"), CanvasOrder = 2 }, // same canvas as above, despite diff order
            new(),
            new() { CanvasOrder = 3 },
            new(),
            new() { CanvasOrder = 1 },
            new() { Id = "one" },
            new() { Id = "one", CanvasOrder = 1 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should()
            .Be(4,
                "1 for CanvasOrder=1, 1 for CanvasOriginalId='https://canvas.ex/1', 1 for CanvasOrder=3, 1 for CanvasOrder=0 (unset)");
    }
}

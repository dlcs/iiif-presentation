using API.Helpers;
using Services.Manifests.Helpers;
using Services.Manifests.Model;

namespace API.Tests.Helpers;

public class InterimCanvasPaintingXTests
{
    [Fact]
    public void GetRequiredNumberOfCanvasIds_0_IfPaintedResourcesNull()
    {
        List<InterimCanvasPainting>? canvasPaintings = null;
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_0_IfPaintedResourcesEmpty()
    {
        var canvasPaintings = new List<InterimCanvasPainting>();
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IfSingleCanvas()
    {
        List<InterimCanvasPainting> canvasPaintings = [new()];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(1);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_AllHaveId()
    {
        List<InterimCanvasPainting> canvasPaintings =
        [
            new() { Id = "1" },
            new() { Id = "2" },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(0);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MultipleCanvasOrder()
    {
        List<InterimCanvasPainting> canvasPaintings =
        [
            new() { CanvasOrder = 1 },
            new() { CanvasOrder = 2 },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_MultipleCanvasOriginalId()
    {
        List<InterimCanvasPainting> canvasPaintings =
        [
            new() { CanvasOriginalId = new Uri("https://canvas.ex/1") },
            new() { CanvasOriginalId = new Uri("https://canvas.ex/2") },
        ];
        canvasPaintings.GetRequiredNumberOfCanvasIds().Should().Be(2);
    }
    
    [Fact]
    public void GetRequiredNumberOfCanvasIds_Correct_IfAllHaveCanvasOrderWithMultipleSame()
    {
        List<InterimCanvasPainting> canvasPaintings =
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
        List<InterimCanvasPainting> canvasPaintings =
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
        List<InterimCanvasPainting> canvasPaintings =
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
        List<InterimCanvasPainting> canvasPaintings =
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
        List<InterimCanvasPainting> canvasPaintings =
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

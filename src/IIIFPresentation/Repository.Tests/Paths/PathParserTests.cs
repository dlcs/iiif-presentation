using IIIF.Presentation.V3;
using Models.API.Manifest;
using Models.DLCS;
using Repository.Paths;

namespace Repository.Tests.Paths;

public class PathParserTests
{
    [Fact]
    public void GetAssetIdFromNamedQueryCanvasId_Correct()
    {
        var canvas = new Canvas { Id = "https://dlcs.example/iiif-img/7/8/foo/canvas/c/10" };
        var expected = new AssetId(7, 8, "foo");

        var actual = canvas.GetAssetIdFromNamedQueryCanvasId();
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetAssetIdFromNamedQueryCanvasId_Throws_IfIdNullOrWhitespace(string? incorrectId)
    {
        var canvas = new Canvas { Id = incorrectId };
        Action act = () => canvas.GetAssetIdFromNamedQueryCanvasId();
        act.Should().Throw<ArgumentException>()
            .WithMessage("Value cannot be null. (Parameter 'Id')");
    }
    
    [Theory]
    [InlineData("https://dlcs.example/canvas/c/10")]
    [InlineData("https://dlcs.example/7/canvases/1230-934")]
    public void GetAssetIdFromNamedQueryCanvasId_Throws_IfInvalidFormat(string incorrectId)
    {
        var canvas = new Canvas { Id = incorrectId };
        Action act = () => canvas.GetAssetIdFromNamedQueryCanvasId();
        act.Should().Throw<FormatException>()
            .WithMessage($"Unable to extract AssetId from {incorrectId}");
    }

    [Theory]
    [InlineData("https://dlcs.example/1/canvases/someId")]
    [InlineData("someId")]
    public void GetCanvasId_RetrievesCorrectCanvasId_whenCalled(string canvasId)
    {
        var canvasPainting = new CanvasPainting()
        {
            CanvasId = canvasId
        };
        
        var canvasFromPathParser = PathParser.GetCanvasId(canvasPainting, 1);
        
        canvasFromPathParser.Should().Be("someId");
    }
    
    [Theory]
    [InlineData("https://dlcs.example/1/canvases/foo/bar/baz", "foo/bar/baz")]
    [InlineData("https://dlcs.example/1/canvases/someId?foo=bar", "someId?foo=bar")]
    [InlineData("foo/bar/baz", "foo/bar/baz")]
    public void GetCanvasId_ThrowsAnError_whenCalledWithMultipleSlashes(string canvasId, string expected)
    {
        var canvasPainting = new CanvasPainting()
        {
            CanvasId = canvasId
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<ArgumentException>()
            .WithMessage($"canvas Id {expected} contains a prohibited character");
    }
    
    [Fact]
    public void GetCanvasId_ThrowsAnError_whenCalledWithNullCanvasId()
    {
        var canvasPainting = new CanvasPainting()
        {
            CanvasId = null
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'canvasPainting')");
    }
    
    [Theory]
    [InlineData("https://dlcs.example/1/random/foo/bar/baz", "Canvas Id /1/random/foo/bar/baz is not valid")]
    [InlineData("https://dlcs.example/1/canvases", "Canvas Id /1/canvases is not valid")]
    public void GetCanvasId_ThrowsAnError_whenCalledWithInvalidUri(string canvasId, string expectedError)
    {
        var canvasPainting = new CanvasPainting()
        {
            CanvasId = canvasId
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<ArgumentException>()
            .WithMessage(expectedError);
    }
}

using IIIF.Presentation.V3;
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
}

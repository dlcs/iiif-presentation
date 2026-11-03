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
    
    [Fact]
    public void GetParentUriFromPublicId_ReturnsSlugFromPath()
    {
        var slug = PathParser.GetParentUriFromPublicId("https://dlcs.example/1/slug/slug");
        
        slug.Should().Be("https://dlcs.example/1/slug");
    }
    
    [Theory]
    [InlineData("https://foo.com/foo/slug", "slug")]
    [InlineData("https://foo.com/slug", "slug")]
    [InlineData("https://foo.com", "")]
    public void GetSlugFromHierarchicalPath_RetrievesSlug(string path, string expected)
    {
        // Arrange and Act
        var slug = PathParser.GetSlugFromHierarchicalPath(path, 1);
        
        // Asset
        slug.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://dlcs.example/adjunct-annotations/7/0/Manifest_foo",
        "https://dlcs.example/annotations/7/Manifest_foo")]
    [InlineData("https://dlcs.example/adjunct-annotations/7/0/Manifest_foo/external",
        "https://dlcs.example/annotations/7/Manifest_foo/external")]
    [InlineData("https://other.example/adjunct-annotations/7/0/Manifest_foo",
        "https://other.example/annotations/7/Manifest_foo")]
    public void GetAdjunctAnnotationIdWithoutSpace_RemovesSpace(string id, string expected)
    {
        var assetId = new AssetId(7, 0, "Manifest_foo");

        var actual = PathParser.GetAdjunctAnnotationIdWithoutSpace(id, assetId);

        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://dlcs.example/adjunct-annotations/8/0/Manifest_foo")]
    [InlineData("https://dlcs.example/adjunct-annotations/7/1/Manifest_foo")]
    [InlineData("https://dlcs.example/adjuncts/7/0/Manifest_foo/annotations")]
    [InlineData("https://dlcs.example/custom/7/0/Manifest_foo")]
    public void GetAdjunctAnnotationIdWithoutSpace_ReturnsUnchanged_IfNotAdjunctAnnotationIdForCustomerAndSpace(
        string id)
    {
        var assetId = new AssetId(7, 0, "Manifest_foo");

        var actual = PathParser.GetAdjunctAnnotationIdWithoutSpace(id, assetId);

        actual.Should().Be(id);
    }

    [Fact]
    public void GetAdjunctAnnotationIdWithoutSpace_ReturnsNull_IfIdNull()
    {
        var actual = PathParser.GetAdjunctAnnotationIdWithoutSpace(null, new AssetId(7, 0, "Manifest_foo"));

        actual.Should().BeNull();
    }
}

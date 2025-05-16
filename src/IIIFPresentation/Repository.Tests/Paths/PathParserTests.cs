using Core.Web;
using Core.Exceptions;
using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging.Abstractions;
using Models.API.Manifest;
using Models.DLCS;
using Repository.Paths;

namespace Repository.Tests.Paths;

public class PathParserTests
{
    private readonly TypedPathTemplateOptions defaultTypedPathTemplateOptions = new ()
    {
        Defaults = new Dictionary<string, string>
        {
            ["ManifestPrivate"] = "/{customerId}/manifests/{resourceId}",
            ["CollectionPrivate"] = "/{customerId}/collections/{resourceId}",
            ["ResourcePublic"] = "/{customerId}/{hierarchyPath}",
            ["Canvas"] = "/{customerId}/canvases/{resourceId}"
        },
        Overrides =
        {
            // override everything
            ["foo.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
            },
            // fallback to defaults
            ["no-customer.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/manifests/{resourceId}",
                ["CollectionPrivate"] = "/collections/{resourceId}",
                ["ResourcePublic"] = "/{hierarchyPath}",
                ["Canvas"] = "/canvases/{resourceId}"
            },
            ["additional-path-no-customer.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/foo/manifests/{resourceId}",
                ["CollectionPrivate"] = "/foo/collections/{resourceId}",
                ["ResourcePublic"] = "/foo/{hierarchyPath}",
                ["Canvas"] = "/foo/canvases/{resourceId}"
            },
            // custom base URL
            ["fully-qualified.com"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "https://foo.com/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "https://foo.com/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "https://foo.com/{customerId}/{hierarchyPath}",
                ["Canvas"] = "https://foo.com/{customerId}/canvases/{resourceId}"
            }
        }
    };
    
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
    public void GetCanvasId_ThrowsAnError_WhenCalledWithMultipleSlashes(string canvasId, string expected)
    {
        var canvasPainting = new CanvasPainting
        {
            CanvasId = canvasId
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<InvalidCanvasIdException>()
            .WithMessage(
                $"Canvas Id {expected} contains a prohibited character. Cannot contain any of: '/','=','=',','");
    }
    
    [Fact]
    public void GetCanvasId_ThrowsAnError_WhenCalledWithNullCanvasId()
    {
        var canvasPainting = new CanvasPainting
        {
            CanvasId = null
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("canvasPainting");
    }
    
    [Theory]
    [InlineData("https://dlcs.example/1/random/foo/bar/baz", "Canvas Id /1/random/foo/bar/baz is not valid")]
    [InlineData("https://dlcs.example/1/canvases", "Canvas Id /1/canvases is not valid")]
    [InlineData("https://dlcs.example/1/canvases/", "Canvas Id /1/canvases/ is not valid")]
    public void GetCanvasId_ThrowsAnError_WhenCalledWithInvalidUri(string canvasId, string expectedError)
    {
        var canvasPainting = new CanvasPainting
        {
            CanvasId = canvasId
        };

        Action act = () =>  PathParser.GetCanvasId(canvasPainting, 1);
        act.Should().Throw<InvalidCanvasIdException>()
            .WithMessage(expectedError);
    }
    
    [Fact]
    public void GetParentUriFromPublicId_ReturnsSlugFromPath()
    {
        var slug = PathParser.GetParentUriFromPublicId("https://dlcs.example/1/slug/slug");
        
        slug.Should().Be("https://dlcs.example/1/slug");
    }

    [Theory]
    [InlineData("1/slug/slug", "slug/slug",false)]
    [InlineData("/1/slug/slug", "slug/slug", false)]
    [InlineData("2/slug/slug", "slug/slug",false, 2)]
    [InlineData("1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug", false)]
    [InlineData("1/manifests/manifest", "manifest", true)]
    [InlineData("1/collections/collection", "collection", true)]
    [InlineData("1/canvases/canvas", "canvas", true)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithStandardDefaults(string path, string resource, bool canonical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = PathParser.ParsePathWithRewrites(defaultTypedPathTemplateOptions, "default-host.com", path,
            customer, new NullLogger<PathParserTests>());
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Canonical.Should().Be(canonical);
    }
    
    [Theory]
    [InlineData("foo/1/slug/slug", "slug/slug",false)]
    [InlineData("/foo/1/slug/slug", "slug/slug", false)]
    [InlineData("/foo/2/slug/slug", "slug/slug",false, 2)]
    [InlineData("foo/1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",false)]
    [InlineData("foo/1/manifests/manifest", "manifest",true)]
    [InlineData("foo/1/collections/collection", "collection", true)]
    [InlineData("foo/1/canvases/canvas", "canvas",true)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithAdditionalPathElement(string path, string resource,
        bool canonical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = PathParser.ParsePathWithRewrites(defaultTypedPathTemplateOptions, "foo.com", path,
            customer, new NullLogger<PathParserTests>());
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Canonical.Should().Be(canonical);
    }
    
    [Theory]
    [InlineData("slug/slug", "slug/slug", false)]
    [InlineData("/slug/slug", "slug/slug",false)]
    [InlineData("slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",false)]
    [InlineData("manifests/manifest", "manifest",true)]
    [InlineData("collections/collection", "collection",true)]
    [InlineData("canvases/canvas", "canvas",true)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithNoCustomer(string path, string resource, bool canonical, 
        int customer = 1)
    {
        // Arrange and Act
        var parsedPath = PathParser.ParsePathWithRewrites(defaultTypedPathTemplateOptions, "no-customer.com", path,
            customer, new NullLogger<PathParserTests>());
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Canonical.Should().Be(canonical);
    }
    
    [Theory]
    [InlineData("foo/slug/slug", "slug/slug",1, false)]
    [InlineData("/foo/slug/slug", "slug/slug",1, false)]
    [InlineData("foo/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",1, false)]
    [InlineData("foo/manifests/manifest", "manifest",1, true)]
    [InlineData("foo/collections/collection", "collection",1, true)]
    [InlineData("foo/canvases/canvas", "canvas",1, true)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithNoCustomerAndAdditionalPathElement(string path, string resource, int customer, bool canonical)
    {
        // Arrange and Act
        var parsedPath = PathParser.ParsePathWithRewrites(defaultTypedPathTemplateOptions, 
            "additional-path-no-customer.com", path, customer, new NullLogger<PathParserTests>());
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Canonical.Should().Be(canonical);
    }
    
    [Theory]
    [InlineData("1/slug/slug", "slug/slug",false)]
    [InlineData("/1/slug/slug", "slug/slug", false)]
    [InlineData("2/slug/slug", "slug/slug",false, 2)]
    [InlineData("1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug", false)]
    [InlineData("1/manifests/manifest", "manifest", true)]
    [InlineData("1/collections/collection", "collection", true)]
    [InlineData("1/canvases/canvas", "canvas", true)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithFullyQualifiedPath(string path, string resource, bool canonical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = PathParser.ParsePathWithRewrites(defaultTypedPathTemplateOptions, "fully-qualified.com", 
            path, customer, new NullLogger<PathParserTests>());
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Canonical.Should().Be(canonical);
    }
    
    [Theory]
    [InlineData("https://foo.com/slug/slug", "slug")]
    [InlineData("https://foo.com/slug", "slug")]
    [InlineData("https://foo.com", "")]
    public void GetSlugFromHierarchicalPath_RetrievesSlug(string path, string expected)
    {
        // Arrange and Act
        var slug = PathParser.GetSlugFromHierarchicalPath(path, 1);
        
        // Asset
        slug.Should().Be(expected);
    }
}

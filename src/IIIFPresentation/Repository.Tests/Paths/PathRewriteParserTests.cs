using Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace Repository.Tests.Paths;

public class PathRewriteParserTests
{
    private readonly PathRewriteParser pathRewriteParser;
    
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

    public PathRewriteParserTests()
    {
        pathRewriteParser = new PathRewriteParser(Options.Create(defaultTypedPathTemplateOptions),
            new NullLogger<PathRewriteParser>());
    }
    
    [Theory]
    [InlineData("1/slug/slug", "slug/slug",true)]
    [InlineData("/1/slug/slug", "slug/slug", true)]
    [InlineData("2/slug/slug", "slug/slug",true, 2)]
    [InlineData("1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug", true)]
    [InlineData("1/manifests/manifest", "manifest", false)]
    [InlineData("1/manifests/hello-world", "hello-world", false)]
    [InlineData("1/collections/collection", "collection", false)]
    [InlineData("1/canvases/canvas", "canvas", false)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithStandardDefaults(string path, string resource, bool hierarchical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites("default-host.com", path,
            customer);
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
    
    [Theory]
    [InlineData("foo/1/slug/slug", "slug/slug",true)]
    [InlineData("/foo/1/slug/slug", "slug/slug", true)]
    [InlineData("/foo/2/slug/slug", "slug/slug",true, 2)]
    [InlineData("foo/1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",true)]
    [InlineData("foo/1/manifests/manifest", "manifest",false)]
    [InlineData("foo/1/collections/collection", "collection", false)]
    [InlineData("foo/1/canvases/canvas", "canvas",false)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithAdditionalPathElement(string path, string resource,
        bool hierarchical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites("foo.com", path,
            customer);
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
    
    [Theory]
    [InlineData("slug/slug", "slug/slug", true)]
    [InlineData("/slug/slug", "slug/slug",true)]
    [InlineData("slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",true)]
    [InlineData("manifests/manifest", "manifest",false)]
    [InlineData("collections/collection", "collection",false)]
    [InlineData("canvases/canvas", "canvas",false)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithNoCustomer(string path, string resource, bool hierarchical, 
        int customer = 1)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites("no-customer.com", path,
            customer);
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
    
    [Theory]
    [InlineData("foo/slug/slug", "slug/slug",1, true)]
    [InlineData("/foo/slug/slug", "slug/slug",1, true)]
    [InlineData("foo/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug",1, true)]
    [InlineData("foo/manifests/manifest", "manifest",1, false)]
    [InlineData("foo/collections/collection", "collection",1, false)]
    [InlineData("foo/canvases/canvas", "canvas",1, false)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithNoCustomerAndAdditionalPathElement(string path, string resource, int customer, bool hierarchical)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites("additional-path-no-customer.com", path, customer);
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
    
    [Theory]
    [InlineData("1/slug/slug", "slug/slug",true)]
    [InlineData("/1/slug/slug", "slug/slug", true)]
    [InlineData("2/slug/slug", "slug/slug",true, 2)]
    [InlineData("1/slug/slug/slug/slug/slug", "slug/slug/slug/slug/slug", true)]
    [InlineData("1/manifests/manifest", "manifest", false)]
    [InlineData("1/collections/collection", "collection", false)]
    [InlineData("1/canvases/canvas", "canvas", false)]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithFullyQualifiedPath(string path, string resource, bool hierarchical, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites("fully-qualified.com", path, customer);
        
        // Asset
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
}

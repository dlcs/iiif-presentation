using Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Test.Helpers.Helpers;

namespace Repository.Tests.Paths;

public class PathRewriteParserTests
{
    private readonly PathRewriteParser pathRewriteParser;

    public PathRewriteParserTests()
    {
        pathRewriteParser = new PathRewriteParser(Options.Create(PathRewriteOptions.Default),
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
        
        // Assert
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
        
        // Assert
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
        
        // Assert
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
        
        // Assert
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
        
        // Assert
        parsedPath.Customer.Should().Be(customer);
        parsedPath.Resource.Should().Be(resource);
        parsedPath.Hierarchical.Should().Be(hierarchical);
    }
    
    [Theory]
    [InlineData("https://additional-path-no-customer.com/foo/canvases/canvas")]
    [InlineData("https://fully-qualified.com/1/slug/slug")]
    public void ParsePathWithRewrites_ParsesPathCorrectly_WithUriString(string path, int customer = 1)
    {
        // Arrange and Act
        var parsedPath = pathRewriteParser.ParsePathWithRewrites(path, customer);
        
        // Assert
        parsedPath.Customer.Should().Be(customer);
    }
}

using API.Paths;
using Core.Web;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Tests.Paths;

public class HostnameDrivenPresentationPathGeneratorTests
{
    private static readonly IHttpContextAccessor HttpContextAccessor = A.Fake<IHttpContextAccessor>();
    private static readonly HttpRequest Request = A.Fake<HttpRequest>();

    private readonly TypedPathTemplateOptions defaultTypedPathTemplateOptions = new ()
    {
        Defaults = new Dictionary<string, string>
        {
            ["ManifestPrivate"] = "/custom/{customerId}/manifests/{resourceId}",
            ["CollectionPrivate"] = "/custom/{customerId}/collections/{resourceId}",
            ["ResourcePublic"] = "/custom/{customerId}/{hierarchyPath}",
            ["Canvas"] = "/custom/{customerId}/canvases/{resourceId}"
        },
        Overrides =
        {
            // override everything
            ["foo"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "/foo/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "/foo/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "/foo/{customerId}/{hierarchyPath}",
                ["Canvas"] = "/foo/{customerId}/canvases/{resourceId}"
            },
            // fallback to defaults
            ["bar"] = new Dictionary<string, string>
            {
                ["ResourcePublic"] = "/bar/{customerId}/{hierarchyPath}"
            },
            // custom base URL
            ["baz"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "https://base/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "https://base/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "https://base/{customerId}/{hierarchyPath}",
                ["Canvas"] = "https://base/{customerId}/canvases/{resourceId}",
            }
        }
    };
    
    public HostnameDrivenPresentationPathGeneratorTests()
    {
        HttpContextAccessor.HttpContext = A.Fake<HttpContext>();

        A.CallTo(() => Request.Host).Returns(new HostString("localhost"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        A.CallTo(() => HttpContextAccessor.HttpContext.Request).Returns(Request);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "http://localhost/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", "http://localhost/1/public/path")]
    public void GetHierarchyPresentationPathForRequest_ReturnsAllPaths_FromEmptyConfig(string resourceType, string hierarchyPath, string expected)
    {
        // Arrange
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(new TypedPathTemplateOptions()),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetHierarchyPresentationPathForRequest(resourceType, 1, hierarchyPath);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.CollectionPrivate, "someId", "http://localhost/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, "someId", "http://localhost/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, "someId", "http://localhost/1/canvases/someId")]
    public void GetFlatPresentationPathForRequest_ReturnsAllPaths_FromEmptyConfig(string resourceType, string resourceId, string expected)
    {
        // Arrange
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(new TypedPathTemplateOptions()),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetFlatPresentationPathForRequest(resourceType, 1, resourceId);

        // Assert
        path.Should().Be(expected);
    }

    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "http://localhost/custom/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", "http://localhost/custom/1/public/path")]
    public void GetHierarchyPresentationPathForRequest_ReturnsAllPaths_FromDefaultConfig(string resourceType, string hierarchyPath, string expected)
    {
        // Arrange
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetHierarchyPresentationPathForRequest(resourceType, 1, hierarchyPath);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.CollectionPrivate, "someId", "http://localhost/custom/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, "someId", "http://localhost/custom/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, "someId", "http://localhost/custom/1/canvases/someId")]
    public void GetFlatPresentationPathForRequest_ReturnsAllPaths_FromDefaultConfig(string resourceType, string resourceId, string expected)
    {
        // Arrange
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetFlatPresentationPathForRequest(resourceType, 1, resourceId);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "http://foo/foo/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", "http://foo/foo/1/public/path")]
    public void GetHierarchyPresentationPathForRequest_ReturnsAllPaths_FromOverrideEverythingConfig(string resourceType, string hierarchyPath, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("foo"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetHierarchyPresentationPathForRequest(resourceType, 1, hierarchyPath);

        // Assert
        path.Should().Be(expected);
    }

    
    [Theory]
    [InlineData(PresentationResourceType.CollectionPrivate, "someId", "http://foo/foo/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, "someId", "http://foo/foo/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, "someId", "http://foo/foo/1/canvases/someId")]
    public void GetFlatPresentationPathForRequest_ReturnsAllPaths_FromOverrideEverythingConfig(string resourceType, string resourceId, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("foo"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetFlatPresentationPathForRequest(resourceType, 1, resourceId);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "https://base/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", "https://base/1/public/path")]
    public void GetHierarchyPresentationPathForRequest_ReturnsAllPaths_FromPartialOverrideConfig(string resourceType, string hierarchyPath, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("baz"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetHierarchyPresentationPathForRequest(resourceType, 1, hierarchyPath);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.CollectionPrivate, "someId", "https://base/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, "someId", "https://base/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, "someId", "https://base/1/canvases/someId")]
    public void GetFlatPresentationPathForRequest_ReturnsAllPaths_FromPartialOverrideConfig(string resourceType, string resourceId, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("baz"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new HostnameDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetFlatPresentationPathForRequest(resourceType, 1, resourceId);

        // Assert
        path.Should().Be(expected);
    }
}

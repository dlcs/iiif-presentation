using API.Paths;
using Core.Web;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace API.Tests.Paths;

public class ConfigDrivenPresentationPathGeneratorTests
{
    private static readonly IHttpContextAccessor HttpContextAccessor = A.Fake<IHttpContextAccessor>();
    private static readonly HttpRequest Request = A.Fake<HttpRequest>();

    private readonly TypedPathTemplateOptions defaultTypedPathTemplateOptions = new ()
    {
        Defaults = new Dictionary<string, string>
        {
            ["ManifestPrivate"] = "custom/{customerId}/manifests/{resourceId}",
            ["CollectionPrivate"] = "custom/{customerId}/collections/{resourceId}",
            ["ResourcePublic"] = "custom/{customerId}/{hierarchyPath}",
            ["Canvas"] = "custom/{customerId}/canvases/{resourceId}"
        },
        Overrides =
        {
            // override everything
            ["foo"] = new Dictionary<string, string>
            {
                ["ManifestPrivate"] = "foo/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "foo/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "foo/{customerId}/{hierarchyPath}",
                ["Canvas"] = "foo/{customerId}/canvases/{resourceId}",
            },
            // fallback to defaults
            ["bar"] = new Dictionary<string, string>
            {
                ["ResourcePublic"] = "bar/{customerId}/{hierarchyPath}",
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
    
    public ConfigDrivenPresentationPathGeneratorTests()
    {
        HttpContextAccessor.HttpContext = A.Fake<HttpContext>();

        A.CallTo(() => Request.Host).Returns(new HostString("localhost"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        A.CallTo(() => HttpContextAccessor.HttpContext.Request).Returns(Request);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", null, "http://localhost/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", null, "http://localhost/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "something", "http://localhost/1/public/path")]
    [InlineData(PresentationResourceType.CollectionPrivate, null, "someId", "http://localhost/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, null, "someId", "http://localhost/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, null, "someId", "http://localhost/1/canvases/someId")]
    public void ConfigDrivenPresentationPathGenerator_ReturnsAllPaths_FromEmptyConfig(string resourceType, string? hierarchyPath, string? resourceId, string expected)
    {
        // Arrange
        var sut = new ConfigDrivenPresentationPathGenerator(Options.Create(new TypedPathTemplateOptions()),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetPresentationPathForRequest(resourceType, 1, hierarchyPath, resourceId);

        // Assert
        path.Should().Be(expected);
    }

    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", null, "http://localhost/custom/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", null, "http://localhost/custom/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "something", "http://localhost/custom/1/public/path")]
    [InlineData(PresentationResourceType.CollectionPrivate, null, "someId", "http://localhost/custom/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, null, "someId", "http://localhost/custom/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, null, "someId", "http://localhost/custom/1/canvases/someId")]
    public void ConfigDrivenPresentationPathGenerator_ReturnsAllPaths_FromDefaultConfig(string resourceType, string? hierarchyPath, string? resourceId, string expected)
    {
        // Arrange
        var sut = new ConfigDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetPresentationPathForRequest(resourceType, 1, hierarchyPath, resourceId);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", null, "http://foo/foo/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", null, "http://foo/foo/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "something", "http://foo/foo/1/public/path")]
    [InlineData(PresentationResourceType.CollectionPrivate, null, "someId", "http://foo/foo/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, null, "someId", "http://foo/foo/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, null, "someId", "http://foo/foo/1/canvases/someId")]
    public void ConfigDrivenPresentationPathGenerator_ReturnsAllPaths_FromOverrideEverythingConfig(string resourceType, string? hierarchyPath, string? resourceId, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("foo"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new ConfigDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetPresentationPathForRequest(resourceType, 1, hierarchyPath, resourceId);

        // Assert
        path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", null, "https://base/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "/public/path", null, "https://base/1/public/path")]
    [InlineData(PresentationResourceType.ResourcePublic, "public/path", "something", "https://base/1/public/path")]
    [InlineData(PresentationResourceType.CollectionPrivate, null, "someId", "https://base/1/collections/someId")]
    [InlineData(PresentationResourceType.ManifestPrivate, null, "someId", "https://base/1/manifests/someId")]
    [InlineData(PresentationResourceType.Canvas, null, "someId", "https://base/1/canvases/someId")]
    public void ConfigDrivenPresentationPathGenerator_ReturnsAllPaths_FromPartialOverrideConfig(string resourceType, string? hierarchyPath, string? resourceId, string expected)
    {
        // Arrange
        A.CallTo(() => Request.Host).Returns(new HostString("baz"));
        A.CallTo(() => Request.Scheme).Returns("http");
        
        var sut = new ConfigDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetPresentationPathForRequest(resourceType, 1, hierarchyPath, resourceId);

        // Assert
        path.Should().Be(expected);
    }
}

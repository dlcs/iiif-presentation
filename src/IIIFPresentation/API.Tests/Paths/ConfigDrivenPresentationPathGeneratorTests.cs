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
    private static readonly HttpRequest request = A.Fake<HttpRequest>();

    private readonly TypedPathTemplateOptions defaultTypedPathTemplateOptions = new ()
    {
        Defaults = new Dictionary<string, string>
        {
            ["ManifestPrivate"] = "custom/{customerId}/manifests/{resourceId}",
            ["CollectionPrivate"] = "custom/{customerId}/collections/{resourceId}",
            ["ResourcePublic"] = "custom/{customerId}/{hierarchyPath}",
            ["Canvas"] = "custom/{customerId}/canvases/{resourceId}",
        },
        Overrides =
        {
            ["foo"] = new Dictionary<string, string>()
            {
                ["ManifestPrivate"] = "foo/{customerId}/manifests/{resourceId}",
                ["CollectionPrivate"] = "foo/{customerId}/collections/{resourceId}",
                ["ResourcePublic"] = "foo/{customerId}/{hierarchyPath}",
                ["Canvas"] = "foo/{customerId}/canvases/{resourceId}",
            },
            ["bar"] = new Dictionary<string, string>()
            {
                ["ResourcePublic"] = "bar/{customerId}/{hierarchyPath}",
            }
        }
    };
    
    public ConfigDrivenPresentationPathGeneratorTests()
    {
        HttpContextAccessor.HttpContext = A.Fake<HttpContext>();

        A.CallTo(() => request.Host).Returns(new HostString("localhost"));
        A.CallTo(() => request.Scheme).Returns("http");
        
        A.CallTo(() => HttpContextAccessor.HttpContext.Request).Returns(request);
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
    public void ConfigDrivenPresentationPathGenerator_ReturnsAllPaths_FromFooOverrideConfig(string resourceType, string? hierarchyPath, string? resourceId, string expected)
    {
        // Arrange
        A.CallTo(() => request.Host).Returns(new HostString("foo"));
        A.CallTo(() => request.Scheme).Returns("http");
        
        var sut = new ConfigDrivenPresentationPathGenerator(Options.Create(defaultTypedPathTemplateOptions),
            HttpContextAccessor);
        
        // Act
        var path = sut.GetPresentationPathForRequest(resourceType, 1, hierarchyPath, resourceId);

        // Assert
        path.Should().Be(expected);
    }
}

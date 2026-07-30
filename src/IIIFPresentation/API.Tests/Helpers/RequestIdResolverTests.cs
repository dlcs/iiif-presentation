using API.Helpers;
using Core.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repository.Paths;
using Services.Manifests.Settings;

namespace API.Tests.Helpers;

public class RequestIdResolverTests
{
    private const int Customer = 1;
    private readonly RequestIdResolver sut;

    public RequestIdResolverTests()
    {
        var pathSettings = new PathSettings
        {
            PresentationApiUrl = new Uri("http://localhost")
        };
        var pathRewriteParser =
            new PathRewriteParser(Options.Create(new TypedPathTemplateOptions()), new NullLogger<PathRewriteParser>());

        sut = new RequestIdResolver(Options.Create(pathSettings), pathRewriteParser);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_ReturnsNone_WhenBodyIdNullOrEmpty(string? bodyId)
    {
        var result = sut.Resolve(Customer, bodyId);

        result.IsError.Should().BeFalse();
        result.FlatId.Should().BeNull();
        result.HierarchicalParentPath.Should().BeNull();
        result.Slug.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenBodyIdIsBareNonUriString()
    {
        var result = sut.Resolve(Customer, "not-a-uri");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().BeNull();
        result.HierarchicalParentPath.Should().BeNull();
        result.Slug.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenHostIsNotRecognised()
    {
        var result = sut.Resolve(Customer, "http://example.com/1/collections/some-id");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsFlatId_ForRecognisedCanonicalCollectionUri()
    {
        var result = sut.Resolve(Customer, "http://localhost/1/collections/some-id");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().Be("some-id");
        result.HierarchicalParentPath.Should().BeNull();
        result.Slug.Should().BeNull();
    }

    [Fact]
    public void Resolve_ReturnsFlatId_ForRecognisedCanonicalManifestUri()
    {
        var result = sut.Resolve(Customer, "http://localhost/1/manifests/some-manifest-id");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().Be("some-manifest-id");
    }

    [Fact]
    public void Resolve_ReturnsHierarchicalParentAndSlug_ForRecognisedHierarchicalUri()
    {
        var result = sut.Resolve(Customer, "http://localhost/1/parent/child-slug");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().BeNull();
        result.HierarchicalParentPath.Should().Be("parent");
        result.Slug.Should().Be("child-slug");
    }

    [Fact]
    public void Resolve_ReturnsError_WhenCustomerInIdDoesNotMatchCaller()
    {
        var result = sut.Resolve(Customer, "http://localhost/2/collections/some-id");

        result.IsError.Should().BeTrue();
        result.Error!.Error.Should()
            .Be("The id has a customer id that does not match the customer id found on the calling URL");
    }

    [Fact]
    public void Resolve_RecognisesCustomerSpecificHost()
    {
        var pathSettings = new PathSettings
        {
            PresentationApiUrl = new Uri("http://localhost"),
            CustomerPresentationApiUrl = { [Customer] = new Uri("http://customer1.example.com") }
        };
        var pathRewriteParser =
            new PathRewriteParser(Options.Create(new TypedPathTemplateOptions()), new NullLogger<PathRewriteParser>());
        var resolver = new RequestIdResolver(Options.Create(pathSettings), pathRewriteParser);

        var result = resolver.Resolve(Customer, "http://customer1.example.com/1/collections/some-id");

        result.IsError.Should().BeFalse();
        result.FlatId.Should().Be("some-id");
    }
}

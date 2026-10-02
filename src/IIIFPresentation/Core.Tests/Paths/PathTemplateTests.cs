using System.ComponentModel;
using Core.Paths;
using FluentAssertions;

namespace Core.Tests.Paths;

public class PathTemplateTests
{
    [Fact]
    public void GeneratePath_ReplacesAllKnownSlugs()
    {
        // Arrange
        PathTemplate template = "/{customerId}/{hierarchyPath}/{resourceId}";

        // Act
        var actual = template.GeneratePath(customer: 99, hierarchyPath: "some/path", resourceId: "abc");

        // Assert
        actual.Should().Be("/99/some/path/abc");
    }

    [Fact]
    public void GeneratePath_ReplacesUnprovidedSlugs_WithEmptyString()
    {
        // Arrange
        PathTemplate template = "/{customerId}/manifests/{resourceId}";

        // Act - no args provided
        var actual = template.GeneratePath();

        // Assert - slugs become empty (double slash is not collapsed), trailing slash trimmed
        actual.Should().Be("//manifests");
    }

    [Fact]
    public void GeneratePath_TrimsLeadingSlashFromHierarchyPath()
    {
        // Arrange
        PathTemplate template = "/{customerId}/{hierarchyPath}";

        // Act
        var actual = template.GeneratePath(customer: 1, hierarchyPath: "/leading/slash");

        // Assert
        actual.Should().Be("/1/leading/slash");
    }

    [Fact]
    public void GeneratePath_TrimsTrailingSlash()
    {
        // Arrange
        PathTemplate template = "/{customerId}/{hierarchyPath}";

        // Act - empty hierarchyPath would leave a trailing slash
        var actual = template.GeneratePath(customer: 5, hierarchyPath: string.Empty);

        // Assert
        actual.Should().Be("/5");
    }

    [Fact]
    public void TemplateParts_SplitsBySlash_RemovingEmptyEntries()
    {
        // Arrange - leading slash produces an empty first entry that should be removed
        PathTemplate template = "/{customerId}/manifests/{resourceId}";

        // Act
        var actual = template.TemplateParts();

        // Assert
        actual.Should().Equal("{customerId}", "manifests", "{resourceId}");
    }

    [Fact]
    public void TemplateParts_TrimsWhitespaceEntries()
    {
        // Arrange
        PathTemplate template = "/ {customerId} / manifests ";

        // Act
        var actual = template.TemplateParts();

        // Assert
        actual.Should().Equal("{customerId}", "manifests");
    }

    [Fact]
    public void TemplateParts_HandlesFullyQualifiedTemplate()
    {
        // Arrange
        PathTemplate template = "https://foo.com/{customerId}/manifests/{resourceId}";

        // Act
        var actual = template.TemplateParts();

        // Assert - "https:" is a segment, the "//" collapses to nothing
        actual.Should().Equal("https:", "foo.com", "{customerId}", "manifests", "{resourceId}");
    }

    [Fact]
    public void TemplateParts_ReturnsEmpty_ForRootTemplate()
    {
        // Arrange
        PathTemplate template = "/";

        // Act
        var actual = template.TemplateParts();

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void ImplicitConversion_FromString_WrapsRawTemplate()
    {
        // Act
        PathTemplate template = "/{customerId}/canvases/{resourceId}";

        // Assert
        template.Template.Should().Be("/{customerId}/canvases/{resourceId}");
    }

    [Fact]
    public void ToString_ReturnsRawTemplate()
    {
        // Arrange
        PathTemplate template = "/{customerId}/manifests/{resourceId}";

        // Assert
        template.ToString().Should().Be("/{customerId}/manifests/{resourceId}");
    }

    [Fact]
    public void Converter_CanConvertFromString()
    {
        // Arrange
        var converter = new PathTemplateConverter();

        // Assert
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }

    [Fact]
    public void Converter_ConvertFrom_String_ProducesPathTemplate()
    {
        // Arrange
        var converter = new PathTemplateConverter();

        // Act
        var actual = converter.ConvertFrom("/{customerId}/manifests/{resourceId}");

        // Assert
        actual.Should().BeOfType<PathTemplate>()
            .Which.Template.Should().Be("/{customerId}/manifests/{resourceId}");
    }

    [Fact]
    public void TypeDescriptor_ResolvesPathTemplateConverter()
    {
        // Asserts the [TypeConverter] attribute is wired - this is what the configuration binder relies on

        // Act
        var converter = TypeDescriptor.GetConverter(typeof(PathTemplate));

        // Assert
        converter.Should().BeOfType<PathTemplateConverter>();
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
    }
}

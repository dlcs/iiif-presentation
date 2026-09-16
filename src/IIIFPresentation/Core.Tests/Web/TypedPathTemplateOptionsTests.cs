using Core.Paths;
using Core.Web;

namespace Core.Tests.Web;

public class TypedPathTemplateOptionsTests
{
    private readonly TypedPathTemplateOptions sut = new()
    {
        Defaults = new Dictionary<string, PathTemplate>
        {
            ["Type1"] = "/path/type1",
            ["Type2"] = "/path/type2"
        },
        Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
        {
            ["proxy.host"] = new()
            {
                ["Type1"] = "/different/type1"
            }
        }
    };

    [Fact]
    public void GetPathTemplateForHostAndType_Throws_IfNoDefaultForType()
    {
        // Act
        Action action = () => sut.GetPathTemplateForHostAndType("default.host", "Type3");
        
        // Assert
        action.Should()
            .Throw<KeyNotFoundException>()
            .WithMessage("Could not find default path template for type: Type3");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsDefault_IfNoHostOverride()
    {
        // Arrange
        const string expected = "/path/type1";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("default.host", "Type1");

        // Assert
        actual.Template.Should().Be(expected, "default is returned if no override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsDefault_IfHostEntry_ButNoServiceOverride()
    {
        // Arrange
        const string expected = "/path/type2";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type2");

        // Assert
        actual.Template.Should().Be(expected, "default is returned as no host-specific override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsOverride_IfFound()
    {
        // Arrange
        const string expected = "/different/type1";

        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type1");

        // Assert
        actual.Template.Should().Be(expected, "type override is returned");
    }

    [Fact]
    public void GetPathTemplatesForHost_ReturnsTemplatesForConfiguredDefaults_WithHostOverridesApplied()
    {
        // Act
        var actual = sut.GetPathTemplatesForHost("proxy.host");

        // Assert
        actual.Should().ContainKeys("Type1", "Type2");
        actual.Should().HaveCount(2, "the configured Defaults drive the set of types returned");
        actual["Type1"].Template.Should().Be("/different/type1", "host override is applied");
        actual["Type2"].Template.Should().Be("/path/type2", "default is used when no host override");
    }

    [Fact]
    public void GetPathTemplatesForHost_ReturnsAllDefaultTypes_WhenDefaultsNotCustomised()
    {
        // Arrange
        var options = new TypedPathTemplateOptions();

        // Act
        var actual = options.GetPathTemplatesForHost("default.host");

        // Assert
        actual.Should().ContainKeys("ManifestPrivate", "CollectionPrivate", "ResourcePublic", "Canvas", "TextServiceJob");
        actual["ManifestPrivate"].Template.Should().Be("/{customerId}/manifests/{resourceId}");
    }

    [Fact]
    public void GetPathTemplateForHostAndType_FallsBackToTextServiceJobDefault_WhenFallbackTypeNotConfigured()
    {
        // Arrange - real (un-customised) defaults, so TextServiceJob's default is present
        var options = new TypedPathTemplateOptions();

        // Act
        var actual = options.GetPathTemplateForHostAndType("default.host", "TextServiceRendering");

        // Assert
        actual.Template.Should().Be("/{customerId}/iiif/{resourceId}",
            "TextServiceRendering has no default of its own, so it falls back to TextServiceJob's");
    }

    [Theory]
    [InlineData("TextServiceSearchService")]
    [InlineData("TextServiceRendering")]
    [InlineData("TextServiceAnnotations")]
    public void GetPathTemplateForHostAndType_FallsBackToHostsTextServiceJobOverride_WhenFallbackTypeNotConfiguredForThatHost(
        string fallbackType)
    {
        // Arrange - host has an override for TextServiceJob, but not for the fallback type itself
        var options = new TypedPathTemplateOptions
        {
            Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
            {
                ["proxy.host"] = new() { ["TextServiceJob"] = "/{resourceId}" }
            }
        };

        // Act
        var actual = options.GetPathTemplateForHostAndType("proxy.host", fallbackType);

        // Assert
        actual.Template.Should().Be("/{resourceId}",
            "falls back to this host's TextServiceJob override, not the global TextServiceJob default");
    }

    [Fact]
    public void GetPathTemplateForHostAndType_UsesOwnOverride_NotTextServiceJobs_WhenExplicitlyConfigured()
    {
        // Arrange - both TextServiceJob and TextServiceRendering are overridden differently for this host
        var options = new TypedPathTemplateOptions
        {
            Overrides = new Dictionary<string, Dictionary<string, PathTemplate>>
            {
                ["proxy.host"] = new()
                {
                    ["TextServiceJob"] = "/{customerId}/iiif/{resourceId}",
                    ["TextServiceRendering"] = "/{customerId}/text/rendering/{resourceId}",
                }
            }
        };

        // Act
        var actual = options.GetPathTemplateForHostAndType("proxy.host", "TextServiceRendering");

        // Assert
        actual.Template.Should().Be("/{customerId}/text/rendering/{resourceId}",
            "TextServiceRendering's own override takes precedence over falling back to TextServiceJob");
    }

    [Fact]
    public void GetPathTemplateForHostAndType_Throws_WhenFallbackTypeAlsoNotConfigured()
    {
        // Arrange - sut's Defaults are fully customised (Type1/Type2 only) so even TextServiceJob's usual
        // default isn't present to fall back to
        // Act
        Action action = () => sut.GetPathTemplateForHostAndType("default.host", "TextServiceRendering");

        // Assert
        action.Should()
            .Throw<KeyNotFoundException>()
            .WithMessage("Could not find default path template for type: TextServiceJob");
    }

    [Fact]
    public void Defaults_IsIndependentCopy_MutatingOneInstanceDoesNotLeakToAnother()
    {
        // Arrange
        var first = new TypedPathTemplateOptions();

        // Act - mutate the first instance's Defaults in place (as config binding does)
        first.Defaults["ManifestPrivate"] = "/mutated/manifest";
        first.Defaults["NewType"] = "/mutated/new";
        var second = new TypedPathTemplateOptions();

        // Assert - a freshly-constructed instance is unaffected by the mutation
        second.Defaults["ManifestPrivate"].Template.Should().Be("/{customerId}/manifests/{resourceId}",
            "each instance gets its own copy of the baseline defaults");
        second.Defaults.Should().NotContainKey("NewType");
    }
}

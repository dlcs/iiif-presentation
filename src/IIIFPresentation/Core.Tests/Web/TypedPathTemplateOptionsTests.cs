using Core.Web;

namespace Core.Tests.Web;

public class TypedPathTemplateOptionsTests
{
    private readonly TypedPathTemplateOptions sut = new()
    {
        Defaults = new Dictionary<string, string>
        {
            ["Type1"] = "/path/type1",
            ["Type2"] = "/path/type2"
        },
        Overrides = new Dictionary<string, Dictionary<string, string>>
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
        actual.Should().Be(expected, "default is returned if no override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsDefault_IfHostEntry_ButNoServiceOverride()
    {
        // Arrange
        const string expected = "/path/type2";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type2");
        
        // Assert
        actual.Should().Be(expected, "default is returned as no host-specific override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsOverride_IfFound()
    {
        // Arrange
        const string expected = "/different/type1";

        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type1");

        // Assert
        actual.Should().Be(expected, "type override is returned");
    }

    [Fact]
    public void GetPathTemplatesForHost_ReturnsTemplatesForConfiguredDefaults_WithHostOverridesApplied()
    {
        // Act
        var actual = sut.GetPathTemplatesForHost("proxy.host");

        // Assert
        actual.Should().ContainKeys("Type1", "Type2");
        actual.Should().HaveCount(2, "the configured Defaults drive the set of types returned");
        actual["Type1"].Should().Be("/different/type1", "host override is applied");
        actual["Type2"].Should().Be("/path/type2", "default is used when no host override");
    }

    [Fact]
    public void GetPathTemplatesForHost_ReturnsAllDefaultTypes_WhenDefaultsNotCustomised()
    {
        // Arrange
        var options = new TypedPathTemplateOptions();

        // Act
        var actual = options.GetPathTemplatesForHost("default.host");

        // Assert
        actual.Should().ContainKeys("ManifestPrivate", "CollectionPrivate", "ResourcePublic", "Canvas");
        actual["ManifestPrivate"].Should().Be("/{customerId}/manifests/{resourceId}");
    }
}

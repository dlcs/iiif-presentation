using Core.Web;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Core.Tests.Web;

/// <summary>
/// Proves the <see cref="PathTemplateConverter"/> lets the configuration binder bind string values (as supplied via
/// appsettings/env vars) into the <see cref="PathTemplate"/>-typed Defaults/Overrides dictionaries.
/// </summary>
public class TypedPathTemplateOptionsBindingTests
{
    [Fact]
    public void Binds_Defaults_And_NestedOverrides_FromConfigurationStrings()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Defaults:ManifestPrivate"] = "/bound/{customerId}/manifests/{resourceId}",
                ["Overrides:proxy.host:ManifestPrivate"] = "/proxy/{customerId}/manifests/{resourceId}",
            })
            .Build();

        // Act
        var options = config.Get<TypedPathTemplateOptions>()!;

        // Assert - string config values were converted into PathTemplate via the TypeConverter
        options.Defaults["ManifestPrivate"].Template.Should().Be("/bound/{customerId}/manifests/{resourceId}");
        options.GetPathTemplateForHostAndType("proxy.host", "ManifestPrivate").Template.Should()
            .Be("/proxy/{customerId}/manifests/{resourceId}");
        options.GetPathTemplateForHostAndType("other.host", "ManifestPrivate").Template.Should()
            .Be("/bound/{customerId}/manifests/{resourceId}", "bound default is used when no host override");
    }
}

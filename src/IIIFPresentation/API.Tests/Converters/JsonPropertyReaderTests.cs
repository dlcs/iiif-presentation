using API.Converters;
using Microsoft.Extensions.Logging.Abstractions;

namespace API.Tests.Converters;

public class JsonPropertyReaderTests
{
    [Fact]
    public void ReadJsonProperty_ReturnsValue_ForCollection()
    {
        JsonPropertyReader.ReadJsonProperty("""{"type": "Collection", "label": {"en": ["foo"]}}""", "type")
            .Should().Be("Collection");
    }

    [Fact]
    public void ReadJsonProperty_ReturnsValue_ForManifest()
    {
        JsonPropertyReader.ReadJsonProperty("""{"type": "Manifest", "label": {"en": ["foo"]}}""", "type")
            .Should().Be("Manifest");
    }

    [Fact]
    public void ReadJsonProperty_ReturnsValue_ForArbitraryPropertyName()
    {
        // not specific to "type" - any top-level string property can be read
        JsonPropertyReader.ReadJsonProperty("""{"slug": "my-slug", "type": "Collection"}""", "slug")
            .Should().Be("my-slug");
    }

    [Fact]
    public void ReadJsonProperty_ReturnsNull_WhenPropertyMissing()
    {
        JsonPropertyReader.ReadJsonProperty("""{"label": {"en": ["foo"]}}""", "type").Should().BeNull();
    }

    [Fact]
    public void ReadJsonProperty_ReturnsNull_WhenPropertyNotAString()
    {
        JsonPropertyReader.ReadJsonProperty("""{"type": 123}""", "type").Should().BeNull();
    }

    [Fact]
    public void ReadJsonProperty_ReturnsNull_ForMalformedJson()
    {
        JsonPropertyReader.ReadJsonProperty("not json at all", "type").Should().BeNull();
    }

    [Fact]
    public void ReadJsonProperty_DoesNotMatchNestedProperty()
    {
        // only the top-level property should be considered - a nested property of the same name (e.g. on a
        // behavior/item) must not be mistaken for the top-level one
        JsonPropertyReader.ReadJsonProperty(
                """{"label": {"en": ["foo"]}, "items": [{"type": "Manifest"}]}""", "type")
            .Should().BeNull();
    }

    [Fact]
    public void ReadJsonProperty_FindsValue_AtRequestedDepth()
    {
        JsonPropertyReader.ReadJsonProperty(
                """{"metadata": {"attributes": {"priority": "high"}}}""", "priority", level: 3)
            .Should().Be("high");
    }

    [Fact]
    public void ReadJsonProperty_DoesNotMatch_AtWrongDepth()
    {
        // "priority" exists, but at depth 3, not depth 1 - a level mismatch should not match
        JsonPropertyReader.ReadJsonProperty(
                """{"metadata": {"attributes": {"priority": "high"}}}""", "priority")
            .Should().BeNull();
    }

    [Fact]
    public void TryGetValidType_ReturnsFalse_WhenMissing()
    {
        const string input = """{"foo": "bar"}""";
        var result = JsonPropertyReader.TryGetValidType(input, new NullLogger<JsonPropertyReaderTests>(), out var type);
        result.Should().BeFalse();
        type.Should().BeNull();
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Other")]
    public void TryGetValidType_ReturnsFalse_WhenNullWhitespaceOrUnknown(string? inputType)
    {
        var input = $$"""{"foo": "bar", "type": "{{inputType}}"}""";
        var result = JsonPropertyReader.TryGetValidType(input, new NullLogger<JsonPropertyReaderTests>(), out var type);
        result.Should().BeFalse();
        type.Should().BeNull();
    }

    [Theory]
    [InlineData("Manifest")]
    [InlineData("Collection")]
    public void TryGetValidType_ReturnsTrue_AndType_WhenFound(string? inputType)
    {
        var input = $$"""{"foo": "bar", "type": "{{inputType}}"}""";
        var result = JsonPropertyReader.TryGetValidType(input, new NullLogger<JsonPropertyReaderTests>(), out var type);
        result.Should().BeTrue();
        type.Should().Be(inputType);
    }
}

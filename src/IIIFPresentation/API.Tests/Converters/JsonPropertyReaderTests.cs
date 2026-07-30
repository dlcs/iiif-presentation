using API.Converters;

namespace API.Tests.Converters;

public class JsonPropertyReaderTests
{
    [Fact]
    public void ReadTopLevelString_ReturnsValue_ForCollection()
    {
        JsonPropertyReader.ReadTopLevelString("""{"type": "Collection", "label": {"en": ["foo"]}}""", "type")
            .Should().Be("Collection");
    }

    [Fact]
    public void ReadTopLevelString_ReturnsValue_ForManifest()
    {
        JsonPropertyReader.ReadTopLevelString("""{"type": "Manifest", "label": {"en": ["foo"]}}""", "type")
            .Should().Be("Manifest");
    }

    [Fact]
    public void ReadTopLevelString_ReturnsValue_ForArbitraryPropertyName()
    {
        // not specific to "type" - any top-level string property can be read
        JsonPropertyReader.ReadTopLevelString("""{"slug": "my-slug", "type": "Collection"}""", "slug")
            .Should().Be("my-slug");
    }

    [Fact]
    public void ReadTopLevelString_ReturnsNull_WhenPropertyMissing()
    {
        JsonPropertyReader.ReadTopLevelString("""{"label": {"en": ["foo"]}}""", "type").Should().BeNull();
    }

    [Fact]
    public void ReadTopLevelString_ReturnsNull_WhenPropertyNotAString()
    {
        JsonPropertyReader.ReadTopLevelString("""{"type": 123}""", "type").Should().BeNull();
    }

    [Fact]
    public void ReadTopLevelString_ReturnsNull_ForMalformedJson()
    {
        JsonPropertyReader.ReadTopLevelString("not json at all", "type").Should().BeNull();
    }

    [Fact]
    public void ReadTopLevelString_DoesNotMatchNestedProperty()
    {
        // only the top-level property should be considered - a nested property of the same name (e.g. on a
        // behavior/item) must not be mistaken for the top-level one
        JsonPropertyReader.ReadTopLevelString(
                """{"label": {"en": ["foo"]}, "items": [{"type": "Manifest"}]}""", "type")
            .Should().BeNull();
    }
}

using Models.API.General;
using Newtonsoft.Json;

namespace Models.Tests.API.General;

public class TagsConverterTests
{
    private class Tagged
    {
        [JsonConverter(typeof(TagsConverter))]
        public List<string>? Tags { get; set; }
    }

    private static List<string>? ReadTags(string json) => JsonConvert.DeserializeObject<Tagged>(json)!.Tags;

    [Fact]
    public void ReadJson_ReadsArray()
        => ReadTags("""{ "tags": ["one", "two"] }""").Should().Equal("one", "two");

    [Fact]
    public void ReadJson_SplitsCommaSeparatedString()
        => ReadTags("""{ "tags": "one, two,three" }""").Should().Equal("one", "two", "three");

    [Fact]
    public void ReadJson_ReadsSingleTagString()
        => ReadTags("""{ "tags": "two words" }""").Should().Equal("two words");

    [Theory]
    [InlineData("""{ "tags": "  one ,, two , " }""")]
    [InlineData("""{ "tags": [" one", "", "two ", null] }""")]
    public void ReadJson_TrimsAndRemovesEmptyEntries(string json)
        => ReadTags(json).Should().Equal("one", "two");

    [Theory]
    [InlineData("""{ "tags": "" }""")]
    [InlineData("""{ "tags": " , " }""")]
    [InlineData("""{ "tags": [] }""")]
    public void ReadJson_ReturnsEmpty_WhenNoTags(string json)
        => ReadTags(json).Should().BeEmpty();

    [Fact]
    public void ReadJson_ReturnsNull_WhenNull()
        => ReadTags("""{ "tags": null }""").Should().BeNull();

    [Fact]
    public void ReadJson_Throws_WhenNotStringOrArray()
    {
        var action = () => ReadTags("""{ "tags": 123 }""");

        action.Should().Throw<JsonSerializationException>();
    }

    [Fact]
    public void WriteJson_WritesArray()
        => JsonConvert.SerializeObject(new Tagged { Tags = ["one", "two"] })
            .Should().Be("""{"Tags":["one","two"]}""");
}

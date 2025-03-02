using System.Text;
using Core.IIIF;
using IIIF.Presentation.V3;
using Models.API.Collection;

namespace Core.Tests.IIIF;

public class IIIFResponseXTests
{
    [Fact]
    public async Task ToPresentation_ReturnsDeserialized_StandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var actual = await stream.ToPresentation<Collection>();

        actual.Id.Should().Be("test-sample");
    }
    
    [Fact]
    public async Task ToPresentation_ReturnsDeserialized_NonStandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\", \"slug\": \"foo\"}";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));

        var actual = await stream.ToPresentation<PresentationCollection>();
        
        actual.Id.Should().Be("test-sample");
        actual.Slug.Should().Be("foo");
    }
    
    [Fact]
    public async Task ToPresentation_Null_IfInvalidJson()
    {
        using var stream = new MemoryStream("not-json"u8.ToArray());
        var actual = await stream.ToPresentation<PresentationCollection>();
        actual.Should().BeNull();
    }
    
    [Fact]
    public async Task ToPresentation_String_ReturnsDeserialized_StandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\"}";

        var actual = await input.ToPresentation<Collection>();

        actual.Id.Should().Be("test-sample");
    }
    
    [Fact]
    public async Task ToPresentation_String_ReturnsDeserialized_NonStandardIIIFModel()
    {
        const string input = "{\"id\": \"test-sample\", \"slug\": \"foo\"}";

        var actual = await input.ToPresentation<PresentationCollection>();
        
        actual.Id.Should().Be("test-sample");
        actual.Slug.Should().Be("foo");
    }

    [Fact]
    public async Task ToPresentation_String_Null_IfInvalidJson()
    {
        var actual = await "not-json".ToPresentation<PresentationCollection>();
        actual.Should().BeNull();
    }
}
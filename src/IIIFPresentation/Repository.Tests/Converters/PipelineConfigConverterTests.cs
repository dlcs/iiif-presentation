using Models.API.Manifest;
using Newtonsoft.Json.Linq;
using Repository.Converters;

namespace Repository.Tests.Converters;

public class PipelineConfigConverterTests
{
    private readonly PipelineConfigConverter sut = new();

    private string ToProvider(PipelineConfig config) =>
        (string)sut.ConvertToProvider(config)!;

    private PipelineConfig FromProvider(string json) =>
        (PipelineConfig)sut.ConvertFromProvider(json)!;

    [Fact]
    public void ConvertToProvider_WritesCamelCase_WithSettingsInline()
    {
        var config = new PipelineConfig
        {
            Action = "Index",
            Settings = new Dictionary<string, JToken>
            {
                ["output"] = new JArray("Search"),
                ["pdfCoverPage"] = "http://cover.page/sample.pdf"
            }
        };

        var json = JObject.Parse(ToProvider(config));

        json.Properties().Select(p => p.Name).Should().BeEquivalentTo("action", "output", "pdfCoverPage");
    }

    [Fact]
    public void RoundTrip_PreservesActionAndSettings()
    {
        var config = new PipelineConfig
        {
            Action = "Index",
            Settings = new Dictionary<string, JToken> { ["output"] = new JArray("Search", "Pdf") }
        };

        var result = FromProvider(ToProvider(config));

        result.Action.Should().Be("Index");
        result.AsTextConfig().Output.Should().BeEquivalentTo("Search", "Pdf");
    }

    [Fact]
    public void ConvertFromProvider_ReadsPascalCaseAction()
    {
        var result = FromProvider("""{"Action":"Index"}""");

        result.Action.Should().Be("Index");
        result.Settings.Should().BeNullOrEmpty();
    }
}

using Models.API.Manifest.Pipelines;
using Newtonsoft.Json.Linq;
using Repository.Converters;

namespace Repository.Tests.Converters;

public class PipelineConfigConverterTests
{
    private readonly PipelineConfigConverter sut = new();

    private string ToProvider(IPipelineConfig config) =>
        (string)sut.ConvertToProvider(config)!;

    private IPipelineConfig FromProvider(string json) =>
        (IPipelineConfig)sut.ConvertFromProvider(json)!;

    [Fact]
    public void ConvertToProvider_WritesCamelCase()
    {
        var config = new TextPipelineConfig
        {
            Action = "Index",
            Output = ["Search"],
            PdfCoverPage = "http://cover.page/sample.pdf"
        };

        var json = JObject.Parse(ToProvider(config));

        json.Properties().Select(p => p.Name).Should().BeEquivalentTo("action", "output", "pdfCoverPage");
    }

    [Fact]
    public void RoundTrip_PreservesTextConfig()
    {
        var config = new TextPipelineConfig
        {
            Action = "Index",
            Output = ["Search", "Pdf"],
            PdfCoverPage = "http://cover.page/sample.pdf"
        };

        var result = FromProvider(ToProvider(config)).Should().BeOfType<TextPipelineConfig>().Subject;

        result.Action.Should().Be("Index");
        result.Output.Should().BeEquivalentTo("Search", "Pdf");
        result.PdfCoverPage.Should().Be("http://cover.page/sample.pdf");
    }

    [Fact]
    public void ConvertFromProvider_ReadsPascalCaseAction()
    {
        var result = FromProvider("""{"Action":"Index"}""").Should().BeOfType<TextPipelineConfig>().Subject;

        result.Action.Should().Be("Index");
        result.Output.Should().BeNull();
    }

    [Fact]
    public void ConvertFromProvider_ReadsGenericConfig_WhenActionHasNoSpecificConfig()
    {
        var result = FromProvider("""{"action":"Other","anything":1}""");

        var config = result.Should().BeOfType<PipelineConfig>().Subject;
        config.Action.Should().Be("Other");
        config.Settings.Should().ContainKey("anything");
    }

    [Fact]
    public void ConvertFromProvider_ReadsGenericConfig_WhenNoAction()
    {
        var result = FromProvider("{}");

        result.Should().BeOfType<PipelineConfig>().Which.Action.Should().BeNull();
    }
}

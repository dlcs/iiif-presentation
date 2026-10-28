using Models.API.Manifest.Pipelines;
using Newtonsoft.Json;

namespace Models.Tests.API.Manifest.Pipelines;

public class PipelineItemConverterTests
{
    private static PipelineItem ReadPipelineItem(string pipelineItemJson) =>
        JsonConvert.DeserializeObject<PipelineItem>(pipelineItemJson)!;

    [Fact]
    public void ReadJson_ReadsTextConfig_RegardlessOfCasing()
    {
        var pipelineItem = ReadPipelineItem(
            """{ "name": "TEXT", "config": { "ACTION": "Index", "OUTPUT": ["Search", "Pdf"], "pdfcoverpage": "http://cover.page/sample.pdf" } }""");

        var config = pipelineItem.Config.Should().BeOfType<TextPipelineConfig>().Subject;
        config.Action.Should().Be("Index");
        config.Output.Should().BeEquivalentTo("Search", "Pdf");
        config.PdfCoverPage.Should().Be("http://cover.page/sample.pdf");
    }

    [Fact]
    public void ReadJson_ReadsConfig_WhenConfigBeforeName()
    {
        var pipelineItem = ReadPipelineItem("""{ "config": { "action": "Index" }, "name": "text" }""");

        pipelineItem.Name.Should().Be("text");
        pipelineItem.Config.Should().BeOfType<TextPipelineConfig>().Which.Action.Should().Be("Index");
    }

    [Fact]
    public void ReadJson_ReadsOtherProperties()
    {
        var pipelineItem = ReadPipelineItem(
            """
            {
                "name": "text",
                "config": { "action": "Index" },
                "status": "Completed",
                "error": "an error",
                "warning": "a warning",
                "created": "2026-10-28T10:00:00Z",
                "finished": "2026-10-28T11:00:00Z"
            }
            """);

        pipelineItem.Name.Should().Be("text");
        pipelineItem.Status.Should().Be("Completed");
        pipelineItem.Error.Should().Be("an error");
        pipelineItem.Warning.Should().Be("a warning");
        pipelineItem.Created.Should().Be(new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc));
        pipelineItem.Finished.Should().Be(new DateTime(2026, 10, 28, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ReadJson_IgnoresUnknownTextSettings()
    {
        var pipelineItem = ReadPipelineItem(
            """{ "name": "text", "config": { "action": "Index", "outptu": ["Search"] } }""");

        var config = pipelineItem.Config.Should().BeOfType<TextPipelineConfig>().Subject;
        config.Action.Should().Be("Index");
        config.Output.Should().BeNull();
    }

    [Fact]
    public void ReadJson_ReadsGenericConfig_WhenTextSettingHasWrongType()
    {
        var pipelineItem = ReadPipelineItem(
            """{ "name": "text", "config": { "action": "Index", "output": "Search" } }""");

        pipelineItem.Config.Should().BeOfType<PipelineConfig>()
            .Which.Settings.Should().ContainKey("output");
    }

    [Fact]
    public void ReadJson_ReadsGenericConfig_ForOtherPipelines()
    {
        var pipelineItem = ReadPipelineItem(
            """{ "name": "ocr", "config": { "action": "Index", "anything": 1 } }""");

        var config = pipelineItem.Config.Should().BeOfType<PipelineConfig>().Subject;
        config.Action.Should().Be("Index");
        config.Settings.Should().ContainKey("anything");
    }

    [Fact]
    public void ReadJson_ReadsGenericConfig_WhenNameMissing()
    {
        var pipelineItem = ReadPipelineItem("""{ "config": { "action": "Index" } }""");

        pipelineItem.Name.Should().BeNull();
        pipelineItem.Config.Should().BeOfType<PipelineConfig>().Which.Action.Should().Be("Index");
    }

    [Fact]
    public void ReadJson_Throws_WhenGenericConfigCannotBeRead()
    {
        var action = () => ReadPipelineItem("""{ "name": "ocr", "config": { "action": {} } }""");

        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void ReadJson_LeavesConfigNull_WhenNotProvided()
    {
        var pipelineItem = ReadPipelineItem("""{ "name": "text", "config": null }""");

        pipelineItem.Name.Should().Be("text");
        pipelineItem.Config.Should().BeNull();
    }

    [Fact]
    public void ReadJson_ReturnsNull_WhenItemIsNull()
    {
        var pipelineItems = JsonConvert.DeserializeObject<List<PipelineItem?>>("[null]");

        pipelineItems.Should().ContainSingle().Which.Should().BeNull();
    }
}

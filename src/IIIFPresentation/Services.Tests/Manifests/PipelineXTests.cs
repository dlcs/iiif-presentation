using Models.API.Manifest;

namespace Services.Tests.Manifests;

public class PipelineXTests
{
    [Fact]
    public void HasTextIndexPipeline_ReturnsFalse_WhenPipelineIsNull()
    {
        var manifest = new PresentationManifest { Pipeline = null };

        manifest.HasTextIndexPipeline().Should().BeFalse();
    }

    [Fact]
    public void HasTextIndexPipeline_ReturnsFalse_WhenPipelineIsEmpty()
    {
        var manifest = new PresentationManifest { Pipeline = [] };

        manifest.HasTextIndexPipeline().Should().BeFalse();
    }

    [Fact]
    public void HasTextIndexPipeline_ReturnsFalse_WhenNameIsNotText()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "ocr", Config = new PipelineConfig { Action = "Index" } }]
        };

        manifest.HasTextIndexPipeline().Should().BeFalse();
    }

    [Fact]
    public void HasTextIndexPipeline_ReturnsFalse_WhenActionIsNotIndex()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Delete" } }]
        };

        manifest.HasTextIndexPipeline().Should().BeFalse();
    }

    [Fact]
    public void HasTextIndexPipeline_ReturnsFalse_WhenConfigIsNull()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = null }]
        };

        manifest.HasTextIndexPipeline().Should().BeFalse();
    }

    [Theory]
    [InlineData("text", "Index")]
    [InlineData("TEXT", "INDEX")]
    [InlineData("Text", "index")]
    public void HasTextIndexPipeline_ReturnsTrue_CaseInsensitive(string name, string action)
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = name, Config = new PipelineConfig { Action = action } }]
        };

        manifest.HasTextIndexPipeline().Should().BeTrue();
    }

    [Fact]
    public void HasTextIndexPipeline_ReturnsTrue_WhenOneOfMultipleItemsMatches()
    {
        var manifest = new PresentationManifest
        {
            Pipeline =
            [
                new PipelineItem { Name = "other", Config = new PipelineConfig { Action = "Do" } },
                new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }
            ]
        };

        manifest.HasTextIndexPipeline().Should().BeTrue();
    }
}
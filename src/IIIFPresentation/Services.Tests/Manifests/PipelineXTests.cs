using Models.API.Manifest;
using Models.Database.General;

namespace Services.Tests.Manifests;

public class PipelineXTests
{
    [Fact]
    public void HasPipelineJob_ReturnsFalse_WhenPipelineIsNull()
    {
        var manifest = new PresentationManifest { Pipeline = null };

        manifest.HasPipelineJob().Should().BeFalse();
    }

    [Fact]
    public void HasPipelineJob_ReturnsFalse_WhenPipelineIsEmpty()
    {
        var manifest = new PresentationManifest { Pipeline = [] };

        manifest.HasPipelineJob().Should().BeFalse();
    }

    [Fact]
    public void HasPipelineJob_ReturnsTrue_WhenPipelineHasAnyItem()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "any", Config = new PipelineConfig { Action = "Do" } }]
        };

        manifest.HasPipelineJob().Should().BeTrue();
    }


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

    [Theory]
    [InlineData(PipelineJobStatus.Waiting, "Waiting")]
    [InlineData(PipelineJobStatus.Completed, "Completed")]
    [InlineData(PipelineJobStatus.Failed, "Failed")]
    public void ToPipelineItem_SetsStatusFromJob(PipelineJobStatus status, string expectedStatus)
    {
        var job = new PipelineJob
        {
            ResourceId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = status,
            Config = new PipelineConfig { Action = "Index" }
        };

        var result = job.ToPipelineItem();

        result.Name.Should().Be("TextService");
        result.Config!.Action.Should().Be("Index");
        result.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void ToPipelineItem_SetsNullConfig_WhenJobConfigIsNull()
    {
        var job = new PipelineJob
        {
            ResourceId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.Queued,
            Config = null
        };

        var result = job.ToPipelineItem();

        result.Config.Should().BeNull();
    }
}

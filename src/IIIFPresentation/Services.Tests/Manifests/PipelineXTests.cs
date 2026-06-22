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
            Status = PipelineJobStatus.Waiting,
            Config = null
        };

        var result = job.ToPipelineItem();

        result.Config.Should().BeNull();
    }
}

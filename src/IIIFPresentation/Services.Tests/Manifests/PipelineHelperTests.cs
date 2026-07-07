using Models.API.Manifest;
using Models.Database.General;

namespace Services.Tests.Manifests;

public class PipelineHelperTests
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
    public void RemoveInvalidPipelines_DoesNothing_WhenPipelineIsNull()
    {
        var manifest = new PresentationManifest { Pipeline = null };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().BeNull();
    }

    [Fact]
    public void RemoveInvalidPipelines_LeavesEmpty_WhenPipelineIsEmpty()
    {
        var manifest = new PresentationManifest { Pipeline = [] };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidPipelines_KeepsValidTextPipeline()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().ContainSingle()
            .Which.Name.Should().Be("text");
    }

    [Theory]
    [InlineData("TEXT")]
    [InlineData("Text")]
    public void RemoveInvalidPipelines_KeepsTextPipeline_RegardlessOfNameCasing(string name)
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = name, Config = new PipelineConfig { Action = "Index" } }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().ContainSingle();
    }

    [Theory]
    [InlineData("index")]
    [InlineData("INDEX")]
    public void RemoveInvalidPipelines_KeepsTextPipeline_RegardlessOfActionCasing(string action)
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = action } }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().ContainSingle();
    }

    [Fact]
    public void RemoveInvalidPipelines_RemovesPipeline_WithUnknownName()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "thumbs", Config = new PipelineConfig { Action = "Index" } }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidPipelines_RemovesTextPipeline_WithInvalidAction()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Delete" } }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidPipelines_RemovesTextPipeline_WithNullConfig()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = null }]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidPipelines_RemovesAllInvalid_KeepingOnlyValid()
    {
        var validItem = new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Index" } };
        var manifest = new PresentationManifest
        {
            Pipeline =
            [
                new PipelineItem { Name = "thumbs", Config = new PipelineConfig { Action = "Index" } },
                validItem,
                new PipelineItem { Name = "text", Config = new PipelineConfig { Action = "Delete" } },
                new PipelineItem { Name = "text", Config = null }
            ]
        };

        manifest.RemoveInvalidPipelines();

        manifest.Pipeline.Should().ContainSingle()
            .Which.Should().BeSameAs(validItem);
    }

    [Fact]
    public void RemoveInvalidPipelines_ReturnsSameManifest_ForFluentChaining()
    {
        var manifest = new PresentationManifest { Pipeline = [] };

        manifest.RemoveInvalidPipelines().Should().BeSameAs(manifest);
    }

    [Theory]
    [InlineData(PipelineJobStatus.Waiting, "Waiting")]
    [InlineData(PipelineJobStatus.Running, "Running")]
    [InlineData(PipelineJobStatus.Completed, "Completed")]
    [InlineData(PipelineJobStatus.Failed, "Failed")]
    [InlineData(PipelineJobStatus.NotSubmitted, "NotSubmitted")]
    public void ToPipelineItem_SetsStatusFromJob(PipelineJobStatus status, string expectedStatus)
    {
        var job = new PipelineJob
        {
            ManifestId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = status,
            Config = new PipelineConfig { Action = "Index" }
        };

        var result = job.ToPipelineItem();

        result.Name.Should().Be(PipelineHelper.TextPipeline.Name);
        result.Config!.Action.Should().Be("Index");
        result.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void ToPipelineItem_SetsNullConfig_WhenJobConfigIsNull()
    {
        var job = new PipelineJob
        {
            ManifestId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.Waiting,
            Config = null
        };

        var result = job.ToPipelineItem();

        result.Config.Should().BeNull();
    }

    [Fact]
    public void ToPipelineItem_SetsErrorCreatedAndFinished_FromJob()
    {
        var created = DateTime.UtcNow.AddMinutes(-5);
        var finished = DateTime.UtcNow;
        var job = new PipelineJob
        {
            ManifestId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.Failed,
            Error = "Something went wrong",
            Created = created,
            Finished = finished
        };

        var result = job.ToPipelineItem();

        result.Error.Should().Be("Something went wrong");
        result.Created.Should().Be(created);
        result.Finished.Should().Be(finished);
    }

    [Theory]
    [InlineData(PipelineJobStatus.Completed, true)]
    [InlineData(PipelineJobStatus.Failed, true)]
    [InlineData(PipelineJobStatus.FailedToSubmit, true)]
    [InlineData(PipelineJobStatus.Waiting, false)]
    [InlineData(PipelineJobStatus.NotSubmitted, false)]
    [InlineData(PipelineJobStatus.Running, false)]
    public void IsFinished_ReturnsExpectedResult_ForEachStatus(PipelineJobStatus status, bool expected)
    {
        status.IsFinished().Should().Be(expected);
    }
}

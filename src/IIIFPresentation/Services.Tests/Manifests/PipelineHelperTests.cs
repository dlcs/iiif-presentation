using Core.IIIF;
using IIIF.Serialisation;
using Models.API.Manifest;
using Models.API.Manifest.Pipelines;
using Models.Database.General;
using Newtonsoft.Json.Linq;

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
            Pipeline = [new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = "Index" } }]
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
            Pipeline = [new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = action } }]
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
            Pipeline = [new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = "Delete" } }]
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
        var validItem = new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = "Index" } };
        var manifest = new PresentationManifest
        {
            Pipeline =
            [
                new PipelineItem { Name = "thumbs", Config = new PipelineConfig { Action = "Index" } },
                validItem,
                new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = "Delete" } },
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
    [InlineData("text")]
    [InlineData("TEXT")]
    public void IsTextPipeline_True_ForTextName_RegardlessOfCasing(string name)
        => new PipelineItem { Name = name }.IsTextPipeline().Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("thumbs")]
    public void IsTextPipeline_False_ForOtherNames(string? name)
        => new PipelineItem { Name = name }.IsTextPipeline().Should().BeFalse();

    [Theory]
    [InlineData(PipelineJobStatus.Waiting, "Waiting")]
    [InlineData(PipelineJobStatus.Running, "Running")]
    [InlineData(PipelineJobStatus.Completed, "Completed")]
    [InlineData(PipelineJobStatus.CompletedNoOperation, "CompletedNoOperation")]
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
    [InlineData(PipelineJobStatus.CompletedNoOperation, true)]
    [InlineData(PipelineJobStatus.Failed, true)]
    [InlineData(PipelineJobStatus.FailedToSubmit, true)]
    [InlineData(PipelineJobStatus.Waiting, false)]
    [InlineData(PipelineJobStatus.NotSubmitted, false)]
    [InlineData(PipelineJobStatus.Running, false)]
    public void IsFinished_ReturnsExpectedResult_ForEachStatus(PipelineJobStatus status, bool expected)
    {
        status.IsFinished().Should().Be(expected);
    }

    [Fact]
    public void ToPipelineItem_SetsWarning_WhenStatusIsCompletedNoOperation()
    {
        var job = new PipelineJob
        {
            ManifestId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = PipelineJobStatus.CompletedNoOperation
        };

        var result = job.ToPipelineItem();

        result.Warning.Should().Be(PipelineHelper.TextPipeline.NoTextFoundWarning);
    }

    [Theory]
    [InlineData(PipelineJobStatus.Completed)]
    [InlineData(PipelineJobStatus.Failed)]
    [InlineData(PipelineJobStatus.Waiting)]
    public void ToPipelineItem_WarningIsNull_ForOtherStatuses(PipelineJobStatus status)
    {
        var job = new PipelineJob
        {
            ManifestId = "id", CustomerId = 1,
            JobType = PipelineJobType.TextService,
            Status = status
        };

        var result = job.ToPipelineItem();

        result.Warning.Should().BeNull();
    }

    [Theory]
    [InlineData("text", typeof(TextPipelineConfig))]
    [InlineData("TEXT", typeof(TextPipelineConfig))]
    [InlineData("ocr", typeof(PipelineConfig))]
    [InlineData(null, typeof(PipelineConfig))]
    public void GetConfigTypeByName_ReturnsExpectedType(string? name, Type expected)
    {
        PipelineHelper.GetConfigTypeByName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Index", typeof(TextPipelineConfig))]
    [InlineData("index", typeof(TextPipelineConfig))]
    [InlineData("Unknown", typeof(PipelineConfig))]
    [InlineData(null, typeof(PipelineConfig))]
    public void GetConfigTypeByAction_ReturnsExpectedType(string? action, Type expected)
    {
        PipelineHelper.GetConfigTypeByAction(action).Should().Be(expected);
    }

    [Fact]
    public async Task TextPipelineConfig_RoundTrips_ThroughApiSerialisation()
    {
        var manifest = (await """
                               {
                                   "type": "Manifest",
                                   "pipeline": [ { "name": "text", "config": { "action": "Index", "output": ["Search"], "pdfCoverPage": "http://cover.page/sample.pdf" } } ]
                               }
                               """.ToPresentation<PresentationManifest>())!;

        var config = JObject.Parse(manifest.AsJson())["pipeline"]![0]!["config"]!;
        config["action"]!.Value<string>().Should().Be("Index");
        config["output"]!.Values<string>().Should().BeEquivalentTo("Search");
        config["pdfCoverPage"]!.Value<string>().Should().Be("http://cover.page/sample.pdf");
    }

    [Fact]
    public void TextPipelineConfig_OmitsUnsetSettings_WhenSerialised()
    {
        var manifest = new PresentationManifest
        {
            Pipeline = [new PipelineItem { Name = "text", Config = new TextPipelineConfig { Action = "Index" } }]
        };

        var config = (JObject)JObject.Parse(manifest.AsJson())["pipeline"]![0]!["config"]!;
        config.Properties().Select(p => p.Name).Should().BeEquivalentTo("action");
    }
}

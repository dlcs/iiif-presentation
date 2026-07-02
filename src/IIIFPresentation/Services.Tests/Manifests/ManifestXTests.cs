using Models.Database.Collections;
using Models.Database.General;

namespace Services.Tests.Manifests;

public class ManifestXTests
{
    [Fact]
    public void IsIngesting_ReturnsFalse_WhenManifestIsNull()
        => ((Manifest?)null).IsIngesting().Should().BeFalse();

    [Fact]
    public void IsIngesting_ReturnsFalse_WhenBatchesIsNull()
        => new Manifest { Id = "x", CustomerId = 1 }.IsIngesting().Should().BeFalse();

    [Fact]
    public void IsIngesting_ReturnsFalse_WhenNoBatches()
        => new Manifest { Id = "x", CustomerId = 1, Batches = [] }.IsIngesting().Should().BeFalse();

    [Theory]
    [InlineData(BatchStatus.Completed)]
    [InlineData(BatchStatus.Unknown)]
    public void IsIngesting_ReturnsFalse_WhenBatchIsNotIngesting(BatchStatus status)
        => new Manifest { Id = "x", CustomerId = 1, Batches = [new Batch { Id = 1, ManifestId = "x", Status = status }] }
            .IsIngesting().Should().BeFalse();

    [Fact]
    public void IsIngesting_ReturnsTrue_WhenBatchIsIngesting()
        => new Manifest { Id = "x", CustomerId = 1, Batches = [new Batch { Id = 1, ManifestId = "x", Status = BatchStatus.Ingesting }] }
            .IsIngesting().Should().BeTrue();

    [Fact]
    public void HasPendingPipelineJob_ReturnsFalse_WhenManifestIsNull()
        => ((Manifest?)null).HasPendingPipelineJob().Should().BeFalse();

    [Fact]
    public void HasPendingPipelineJob_ReturnsFalse_WhenPipelineJobsIsNull()
        => new Manifest { Id = "x", CustomerId = 1 }.HasPendingPipelineJob().Should().BeFalse();

    [Fact]
    public void HasPendingPipelineJob_ReturnsFalse_WhenNoPipelineJobs()
        => ManifestWithJobs().HasPendingPipelineJob().Should().BeFalse();

    [Theory]
    [InlineData(PipelineJobStatus.Completed)]
    [InlineData(PipelineJobStatus.Failed)]
    [InlineData(PipelineJobStatus.FailedToSubmit)]
    public void HasPendingPipelineJob_ReturnsFalse_WhenJobIsNotQueued(PipelineJobStatus status)
        => ManifestWithJobs(status).HasPendingPipelineJob().Should().BeFalse();

    [Fact]
    public void HasPendingPipelineJob_ReturnsTrue_WhenJobIsQueued()
        => ManifestWithJobs(PipelineJobStatus.Waiting).HasPendingPipelineJob().Should().BeTrue();

    [Fact]
    public void HasPendingPipelineJob_ReturnsTrue_WhenJobIsNotSubmitted()
        => ManifestWithJobs(PipelineJobStatus.NotSubmitted).HasPendingPipelineJob().Should().BeTrue();

    [Fact]
    public void HasFurtherWork_ReturnsFalse_WhenNoIngestingBatchAndNoPendingJob()
        => new Manifest { Id = "x", CustomerId = 1 }.HasFurtherWork().Should().BeFalse();

    [Fact]
    public void HasFurtherWork_ReturnsTrue_WhenBatchIsIngesting()
    {
        var manifest = new Manifest
        {
            Id = "x", CustomerId = 1,
            Batches = [new Batch { Id = 1, ManifestId = "x", Status = BatchStatus.Ingesting }]
        };

        manifest.HasFurtherWork().Should().BeTrue();
    }

    [Fact]
    public void HasFurtherWork_ReturnsTrue_WhenPipelineJobIsQueued()
        => ManifestWithJobs(PipelineJobStatus.Waiting).HasFurtherWork().Should().BeTrue();

    [Fact]
    public void HasFurtherWork_ReturnsTrue_WhenPipelineJobIsNotSubmitted()
        => ManifestWithJobs(PipelineJobStatus.NotSubmitted).HasFurtherWork().Should().BeTrue();

    [Fact]
    public void HasFurtherWork_ReturnsFalse_WhenBatchCompletedAndJobCompleted()
    {
        var manifest = new Manifest
        {
            Id = "x", CustomerId = 1,
            Batches = [new Batch { Id = 1, ManifestId = "x", Status = BatchStatus.Completed }],
            PipelineJobs = [new PipelineJob { ManifestId = "x", CustomerId = 1, Status = PipelineJobStatus.Completed }]
        };

        manifest.HasFurtherWork().Should().BeFalse();
    }

    private static Manifest ManifestWithJobs(PipelineJobStatus? status = null)
    {
        var manifest = new Manifest { Id = "x", CustomerId = 1 };
        if (status.HasValue)
        {
            manifest.PipelineJobs =
            [
                new PipelineJob { ManifestId = "x", CustomerId = 1, Status = status.Value }
            ];
        }
        else
        {
            manifest.PipelineJobs = [];
        }
        return manifest;
    }
}

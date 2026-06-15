using Models.Database.General;

namespace Services.Tests.Manifests;

public class PipelineJobXTests
{
    [Fact]
    public void GetJobId_ReturnsExpectedFormat_ForTextService()
    {
        var job = new PipelineJob
        {
            ResourceId = "my-manifest",
            ResourceType = ResourceType.IIIFManifest,
            JobType = PipelineJobType.TextService,
            CustomerId = 99
        };

        job.GetJobId().Should().Be("99/iiif/my-manifest");
    }

    [Fact]
    public void GetJobId_Throws_ForUnknownJobType()
    {
        var job = new PipelineJob
        {
            ResourceId = "x",
            ResourceType = ResourceType.IIIFManifest,
            JobType = (PipelineJobType)999,
            CustomerId = 1
        };

        job.Invoking(j => j.GetJobId()).Should().Throw<ArgumentOutOfRangeException>();
    }

}
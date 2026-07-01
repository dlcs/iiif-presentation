using Models.Database.General;
using Services.TextServices;

namespace Services.Tests.Manifests;

public class PipelineJobXTests
{
    [Fact]
    public void GetJobId_ReturnsExpectedFormat_ForTextService()
    {
        var job = new PipelineJob
        {
            ManifestId = "my-manifest",
            JobType = PipelineJobType.TextService,
            CustomerId = 99
        };

        job.GetJobId().ToString().Should().Be("99/iiif/my-manifest");
    }

    [Fact]
    public void GetJobId_Throws_ForUnknownJobType()
    {
        var job = new PipelineJob
        {
            ManifestId = "x",
            JobType = (PipelineJobType)999,
            CustomerId = 1
        };

        job.Invoking(j => j.GetJobId()).Should().Throw<ArgumentOutOfRangeException>();
    }
}

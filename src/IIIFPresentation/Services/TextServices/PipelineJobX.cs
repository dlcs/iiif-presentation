using Models.Database.General;

namespace Services.TextServices;

public static class PipelineJobX
{
    public static TextJobId GetJobId(this PipelineJob job) => job.JobType switch
    {
        PipelineJobType.TextService => new TextJobId(job.CustomerId,
            job.ResourceId ?? throw new InvalidOperationException("PipelineJob has no ResourceId")),
        _ => throw new ArgumentOutOfRangeException(nameof(job.JobType), $"Unknown job type: {job.JobType}")
    };
}

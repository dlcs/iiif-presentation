using Models.API.Manifest;

namespace Models.Database.General;

public class PipelineJob : ICustomerEntity
{
    public int Id { get; set; }

    public required string ResourceId { get; set; }

    public ResourceType ResourceType { get; set; }

    public PipelineJobType JobType { get; set; }

    public required int CustomerId { get; set; }

    public PipelineJobStatus Status { get; set; }

    public string? Error { get; set; }

    public DateTime Created { get; set; }

    public DateTime? Finished { get; set; }

    public PipelineConfig? Config { get; set; }
}

public enum PipelineJobStatus
{
    Waiting = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum PipelineJobType
{
    TextService = 0
}

public static class PipelineJobX
{
    public static string GetJobId(this PipelineJob job) => job.JobType switch
    {
        PipelineJobType.TextService => $"{job.CustomerId}/iiif/{job.ResourceId}",
        _ => throw new ArgumentOutOfRangeException(nameof(job.JobType), $"Unknown job type: {job.JobType}")
    };

}

using Models.API.Manifest;
using Models.Database.Collections;

namespace Models.Database.General;

public class PipelineJob : ICustomerEntity
{
    public int Id { get; set; }

    public string? ManifestId { get; set; }

    public virtual Manifest? Manifest { get; set; }

    public string? CollectionId { get; set; }

    public virtual Collection? Collection { get; set; }

    /// <summary>
    /// Id of related Manifest or Collection
    /// </summary>
    public string? ResourceId => ManifestId ?? CollectionId;

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

    /// <summary>
    /// Parses a job id of the form "{customerId}/iiif/{resourceId}" into its components.
    /// Returns null for either component if the format is not recognised.
    /// </summary>
    public static (int? CustomerId, string? ResourceId) ParseJobId(string jobId)
    {
        var firstSlash = jobId.IndexOf('/');
        if (firstSlash <= 0 || !int.TryParse(jobId[..firstSlash], out var customerId)) return (null, null);
        var secondSlash = jobId.IndexOf('/', firstSlash + 1);
        var resourceId = secondSlash > 0 && secondSlash < jobId.Length - 1 ? jobId[(secondSlash + 1)..] : null;
        return (customerId, resourceId);
    }
}

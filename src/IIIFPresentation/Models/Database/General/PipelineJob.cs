using Models.Database.Collections;

namespace Models.Database.General;

public class PipelineJob : ICustomerEntity
{
    public int Id { get; set; }

    public required string ManifestId { get; set; }

    public required int CustomerId { get; set; }

    /// <summary>
    /// Text-services job identifier, format: "{customerId}/iiif/{manifestId}"
    /// </summary>
    public required string TextJobId { get; set; }

    public PipelineJobStatus Status { get; set; }

    public string? Error { get; set; }

    public DateTime Created { get; set; }

    public DateTime? Finished { get; set; }

    public Manifest? Manifest { get; set; }
}

public enum PipelineJobStatus
{
    Queued = 0,
    Completed = 1,
    Failed = 2
}
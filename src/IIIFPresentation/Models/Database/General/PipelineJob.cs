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

    /// <summary>
    /// Identifier for this specific invocation of the pipeline, as assigned by whatever service ran it, so a
    /// completion notification can be matched back to this exact record. For text-services this is currently
    /// its numeric `InvocationCount` (as a string); other pipeline types may use a different scheme (a GUID,
    /// an opaque run id, etc). Null until a submission actually succeeds - <see cref="PipelineJobStatus.NotSubmitted"/>
    /// and <see cref="PipelineJobStatus.FailedToSubmit"/> jobs never get a real value, so they stay null rather
    /// than colliding on a placeholder.
    /// </summary>
    public string? InvocationId { get; set; }
}

public enum PipelineJobStatus
{
    Waiting = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    NotSubmitted = 1000,
    FailedToSubmit = 1001,
    CompletedNoOperation = 1002
}

public enum PipelineJobType
{
    TextService = 0
}

public static class PipelineJobStatusX
{
    /// <summary>
    /// Whether this status represents a job that has stopped processing, successfully or not.
    /// </summary>
    public static bool IsFinished(this PipelineJobStatus status) =>
        status is PipelineJobStatus.Completed or PipelineJobStatus.CompletedNoOperation
            or PipelineJobStatus.Failed or PipelineJobStatus.FailedToSubmit;
}

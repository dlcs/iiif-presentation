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
    Failed = 3,
    NotSubmitted = 4
}

public enum PipelineJobType
{
    TextService = 0
}

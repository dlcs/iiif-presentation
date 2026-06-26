using Models.Database.General;

namespace Services.TextServices;

public interface ITextBuilderClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// </summary>
    /// <param name="job">The pipeline job to submit</param>
    /// <param name="bucket">S3 bucket containing the staged manifest</param>
    /// <param name="resourceKey">S3 key of the staged manifest</param>
    Task<bool> CreateOrUpdateJob(PipelineJob job, string bucket, string resourceKey, CancellationToken cancellationToken);
}

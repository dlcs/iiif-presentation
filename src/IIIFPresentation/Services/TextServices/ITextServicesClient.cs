using IIIF.Presentation.V3;
using Models.Database.General;

namespace Services.TextServices;

public interface ITextServicesClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// </summary>
    /// <param name="job">The pipeline job to submit</param>
    /// <param name="bucket">S3 bucket containing the staged manifest</param>
    /// <param name="resourceKey">S3 key of the staged manifest</param>
    Task<bool> CreateOrUpdateJob(PipelineJob job, string bucket, string resourceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve the text-augmented manifest for a completed job.
    /// Returns null if the job produced no text resources.
    /// </summary>
    /// <param name="jobId">Job identifier in format "{customerId}/iiif/{manifestId}"</param>
    Task<Manifest?> GetTextAugmentedManifest(string jobId, CancellationToken cancellationToken = default);
}

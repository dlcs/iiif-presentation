using IIIF.Presentation.V3;

namespace Services.TextServices;

public interface ITextServicesClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// </summary>
    /// <param name="jobId">Job identifier in format "{customerId}/iiif/{manifestId}"</param>
    /// <param name="sourceS3Uri">S3 URI of the staged manifest, e.g. "s3://bucket/staging/..."</param>
    Task<bool> CreateOrUpdateJob(string jobId, string sourceS3Uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve the text-augmented manifest for a completed job.
    /// Returns null if the job produced no text resources.
    /// </summary>
    /// <param name="jobId">Job identifier in format "{customerId}/iiif/{manifestId}"</param>
    Task<Manifest?> GetTextAugmentedManifest(string jobId, CancellationToken cancellationToken = default);
}
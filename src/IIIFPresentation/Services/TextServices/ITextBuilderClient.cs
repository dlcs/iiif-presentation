using Models.Database.General;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

public interface ITextBuilderClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// Sets <paramref name="job"/>'s <see cref="PipelineJob.Status"/> to <see cref="PipelineJobStatus.Waiting"/> on
    /// success, or <see cref="PipelineJobStatus.FailedToSubmit"/> on failure. On success, also sets
    /// <see cref="PipelineJob.InvocationId"/> from text-services' own (authoritative) counter, as returned in
    /// the response body. On failure, sets <see cref="PipelineJob.Error"/> from the response body (or a fallback
    /// message describing the failure, if there was no body to read).
    /// </summary>
    /// <param name="manifest">The manifest whose staged source the job will read</param>
    /// <param name="job">The pipeline job to submit</param>
    Task<bool> UpsertJob(DbManifest manifest, PipelineJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Delete a text-builder job. A job that no longer exists in text-builder is treated as a success.
    /// </summary>
    /// <param name="jobId">Id of the job to delete</param>
    Task<bool> DeleteJob(TextJobId jobId, CancellationToken cancellationToken);
}

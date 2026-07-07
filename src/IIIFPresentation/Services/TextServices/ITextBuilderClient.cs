using Models.Database.General;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

public interface ITextBuilderClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// Sets <paramref name="job"/>'s <see cref="PipelineJob.Status"/> to <see cref="PipelineJobStatus.Waiting"/> on
    /// success, or <see cref="PipelineJobStatus.FailedToSubmit"/> on failure. On success, also sets
    /// <see cref="PipelineJob.InvocationCount"/> from text-services' own (authoritative) counter, as returned in
    /// the response body.
    /// </summary>
    /// <param name="manifest">The manifest whose staged source the job will read</param>
    /// <param name="job">The pipeline job to submit</param>
    Task<bool> UpsertJob(DbManifest manifest, PipelineJob job, CancellationToken cancellationToken);
}

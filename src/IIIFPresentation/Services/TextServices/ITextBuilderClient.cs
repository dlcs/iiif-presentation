using Models.Database.General;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

public interface ITextBuilderClient
{
    /// <summary>
    /// Create a new text-builder job, or reprocess an existing one.
    /// </summary>
    /// <param name="manifest">The manifest whose staged source the job will read</param>
    /// <param name="job">The pipeline job to submit</param>
    Task<bool> UpsertJob(DbManifest manifest, PipelineJob job, CancellationToken cancellationToken);
}

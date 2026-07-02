using Models.API.Manifest;
using Models.Database.General;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

public interface IPipelineJobService
{
    /// <summary>
    /// Builds a job for the first recognised pipeline step, persists it against the manifest, and saves changes.
    /// Returns null if no recognised pipeline type was found (nothing registered).
    /// </summary>
    Task<PipelineJob?> PersistPipelineJob(DbManifest dbManifest, List<PipelineItem> pipeline,
        CancellationToken cancellationToken);

    /// <summary>
    /// Submits a pipeline job to text-services. The job's <see cref="PipelineJob.Status"/> is updated by the
    /// underlying <see cref="ITextBuilderClient"/> call; callers are responsible for saving that change.
    /// </summary>
    Task<bool> SubmitPipelineJob(DbManifest dbManifest, PipelineJob job, CancellationToken cancellationToken);
}
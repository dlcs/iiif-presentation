using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.API.Manifest;
using Models.Database.General;
using Repository;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

/// <summary>
/// Owns the lifecycle of a text-services <see cref="PipelineJob"/>: building and persisting the DB record, and
/// submitting it to text-services.
/// </summary>
public class PipelineJobService(
    PresentationContext dbContext,
    ITextBuilderClient textBuilderClient,
    ILogger<PipelineJobService> logger) : IPipelineJobService
{
    public async Task<PipelineJob?> PersistPipelineJob(DbManifest dbManifest, List<PipelineItem> pipeline,
        CancellationToken cancellationToken)
    {
        var job = BuildPipelineJob(dbManifest, pipeline);
        if (job == null)
        {
            logger.LogWarning("No recognised pipeline type for manifest {ManifestId}; ignoring pipeline", dbManifest.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        (dbManifest.PipelineJobs ??= []).Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<bool> SubmitPipelineJob(DbManifest dbManifest, PipelineJob job,
        CancellationToken cancellationToken)
    {
        if (await textBuilderClient.UpsertJob(dbManifest, job, cancellationToken)) return true;

        logger.LogError("Failed to submit {JobType} pipeline job for manifest {ManifestId}", job.JobType, dbManifest.Id);
        return false;
    }

    public async Task DeletePipelineJob(DbManifest dbManifest, CancellationToken cancellationToken)
    {
        var hasJob = await dbContext.PipelineJobs
            .AnyAsync(p => p.ManifestId == dbManifest.Id && p.CustomerId == dbManifest.CustomerId, cancellationToken);
        if (!hasJob) return;

        var jobId = new TextJobId(dbManifest.CustomerId, dbManifest.Id);
        if (!await textBuilderClient.DeleteJob(jobId, cancellationToken))
        {
            logger.LogWarning("Failed to delete text-services job {JobId} for manifest {ManifestId}", jobId,
                dbManifest.Id);
        }
    }

    private static PipelineJob? BuildPipelineJob(DbManifest dbManifest, List<PipelineItem> pipeline)
    {
        // Returns a job for the first recognised pipeline step; additional steps of the same type are ignored.
        foreach (var pipelineItem in pipeline)
        {
            if (string.Equals(pipelineItem.Name, PipelineHelper.TextPipeline.Name, StringComparison.OrdinalIgnoreCase))
            {
                return new PipelineJob
                {
                    ManifestId = dbManifest.Id,
                    JobType = PipelineJobType.TextService,
                    CustomerId = dbManifest.CustomerId,
                    Status = PipelineJobStatus.NotSubmitted,
                    Config = pipelineItem.Config,
                    Created = DateTime.UtcNow
                };
            }
        }
        return null;
    }
}

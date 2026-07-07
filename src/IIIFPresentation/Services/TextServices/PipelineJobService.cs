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
        // Each resubmission gets its own PipelineJob row (kept for history) but text-services reuses the
        // same job id and increments its own InvocationCount on every reprocess - mirror that count here so
        // a completion notification can be tied back to this exact row.
        var invocationCount = await dbContext.PipelineJobs
            .Where(p => p.ManifestId == dbManifest.Id && p.JobType == PipelineJobType.TextService)
            .CountAsync(cancellationToken) + 1;

        var job = BuildPipelineJob(dbManifest, pipeline, invocationCount);
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

    private static PipelineJob? BuildPipelineJob(DbManifest dbManifest, List<PipelineItem> pipeline,
        int invocationCount)
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
                    Created = DateTime.UtcNow,
                    InvocationCount = invocationCount
                };
            }
        }
        return null;
    }
}
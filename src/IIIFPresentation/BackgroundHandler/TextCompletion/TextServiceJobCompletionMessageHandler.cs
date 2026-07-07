using System.Diagnostics;
using AWS.SQS;
using BackgroundHandler.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Services.Manifests.AWS;
using Services.TextServices;

namespace BackgroundHandler.TextCompletion;

public class TextServiceJobCompletionMessageHandler(
    PresentationContext dbContext,
    ICustomerIdProvider customerIdProvider,
    IManifestStorageManager manifestStorageManager,
    ITextManifestAugmentor textManifestAugmentor,
    ILogger<TextServiceJobCompletionMessageHandler> logger)
    : MessageHandlerBase<TextServiceJobCompletionMessage>(logger)
{
    protected override TextServiceJobCompletionMessage DeserializeMessage(QueueMessage message) =>
        TextServiceJobCompletionMessage.FromQueueMessage(message);

    protected override async Task<bool> HandleMessage(TextServiceJobCompletionMessage completionMessage,
        QueueMessage rawMessage, CancellationToken cancellationToken)
    {
        if (!TextJobId.TryParse(completionMessage.JobId, out var jobId))
        {
            Logger.LogWarning("Could not parse job id {JobId}; discarding message", completionMessage.JobId);
            return true;
        }

        customerIdProvider.SetCustomerId(jobId!.CustomerId);
        return await TryCompleteManifest(completionMessage, jobId, rawMessage.ApproximateReceiveCount,
            cancellationToken);
    }

    private async Task<bool> TryCompleteManifest(TextServiceJobCompletionMessage completionMessage, TextJobId jobId,
        int approximateReceiveCount, CancellationToken cancellationToken)
    {
        var candidateJobs = await dbContext.PipelineJobs
            .Where(p => p.ManifestId == jobId.ResourceId && p.JobType == PipelineJobType.TextService)
            .Include(p => p.Manifest)
            .OrderByDescending(p => p.Created)
            .ToListAsync(cancellationToken);

        // Match the specific invocation this notification refers to; fall back to "newest wins" for any
        // job created before InvocationCount existed, or if text-services ever omits it.
        var pipelineJob = candidateJobs.FirstOrDefault(p => p.InvocationCount == completionMessage.InvocationCount);
        if (pipelineJob == null && candidateJobs.Count > 0)
        {
            Logger.LogWarning(
                "No PipelineJob matched InvocationCount {InvocationCount} for job {JobId}; falling back to newest of {Count} candidates",
                completionMessage.InvocationCount, completionMessage.JobId, candidateJobs.Count);
            pipelineJob = candidateJobs.FirstOrDefault();
        }

        if (pipelineJob == null)
        {
            return DiscardUntrackedResource(approximateReceiveCount, $"PipelineJob for {completionMessage.JobId}");
        }

        if (pipelineJob is { Status: PipelineJobStatus.Completed, Finished: not null })
        {
            // This should never happen, but if it does, we want to reprocess it to avoid Manifest stuck in "staging"
            Logger.LogWarning("PipelineJob for {JobId} already completed at {Finished}; reprocessing",
                completionMessage.JobId, pipelineJob.Finished);
        }

        var dbManifest = pipelineJob.Manifest;

        if (dbManifest == null)
        {
            Logger.LogError("Manifest {ResourceId} for pipeline job {JobId} not found",
                pipelineJob.ResourceId, completionMessage.JobId);
            return false;
        }

        Logger.LogInformation(
            "Completing text pipeline for job:{JobId}, customer:{CustomerId}, manifest:{ManifestId}",
            completionMessage.JobId, pipelineJob.CustomerId, pipelineJob.ResourceId);

        if (!completionMessage.IsCompleted)
        {
            Logger.LogWarning("Text-services job {JobId} incomplete, status {Status}: {Errors}",
                completionMessage.JobId, completionMessage.Status, completionMessage.Errors);
            pipelineJob.Error = completionMessage.Errors;
            pipelineJob.Status = completionMessage.Status; // This will likely be "Failed" but record what we were given 
            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
            await manifestStorageManager.DeleteStagedManifest(dbManifest);
            return true;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var staged = await manifestStorageManager.ReadStagedManifest(dbManifest, cancellationToken);

            if (staged.Manifest == null)
            {
                Logger.LogError("Staged manifest not found for {ManifestId}; cannot complete text pipeline", dbManifest.Id);
                return false;
            }

            var finalManifest = await textManifestAugmentor.Augment(staged.Manifest, dbManifest, cancellationToken);
            pipelineJob.Status = PipelineJobStatus.Completed;
            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;

            await manifestStorageManager.SaveManifestInStorage(finalManifest, dbManifest, staged.Original,
                saveToStaging: false, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await manifestStorageManager.DeleteStagedManifest(dbManifest);
            Logger.LogInformation(
                "Text pipeline completed for job:{JobId}, manifest:{ManifestId}. Elapsed:{Elapsed}ms",
                completionMessage.JobId, pipelineJob.ResourceId, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error completing text pipeline for job {JobId}", completionMessage.JobId);
            return false;
        }

        return true;
    }
}

using System.Diagnostics;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using IIIF;
using IIIF.Presentation.V3;
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
    IIIIFS3Service iiifS3,
    ITextServicesClient textServicesClient,
    ILogger<TextServiceJobCompletionMessageHandler> logger)
    : IMessageHandler
{
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(TextServiceJobCompletionMessageHandler), message.MessageId))
        {
            try
            {
                var completionMessage = DeserializeMessage(message, logger);
                customerIdProvider.SetCustomerId(ExtractCustomerIdFromJobId(completionMessage.JobId));
                return await TryCompleteManifest(completionMessage, message.ApproximateReceiveCount, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling text-service job completion message {MessageId}", message.MessageId);
            }
        }

        return false;
    }

    private async Task<bool> TryCompleteManifest(TextServiceJobCompletionMessage completionMessage,
        int approximateReceiveCount, CancellationToken cancellationToken)
    {
        var pipelineJob = await dbContext.PipelineJobs
            .Include(p => p.Manifest)
            .ThenInclude(m => m!.CanvasPaintings)
            .SingleOrDefaultAsync(p => p.TextJobId == completionMessage.JobId, cancellationToken);

        if (pipelineJob == null)
        {
            var discard = approximateReceiveCount >= 2;
            logger.LogTrace(
                "PipelineJob for {JobId} not found. ApproximateReceiveCount:{Count}. {Action}",
                completionMessage.JobId, approximateReceiveCount, discard ? "Discarding" : "Will retry");
            return discard;
        }

        var sw = Stopwatch.StartNew();
        var dbManifest = pipelineJob.Manifest!;

        logger.LogInformation(
            "Completing text pipeline for job:{JobId}, customer:{CustomerId}, manifest:{ManifestId}",
            completionMessage.JobId, pipelineJob.CustomerId, pipelineJob.ManifestId);

        try
        {
            var stagedManifest =
                await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, BucketLocationType.Staging, cancellationToken);

            if (stagedManifest == null)
            {
                logger.LogError("Staged manifest not found for {ManifestId}; cannot complete text pipeline", dbManifest.Id);
                return false;
            }

            if (!completionMessage.IsCompleted)
            {
                logger.LogWarning("Text-services job {JobId} failed: {Errors}", completionMessage.JobId,
                    completionMessage.Errors);
                pipelineJob.Error = completionMessage.Errors;
                pipelineJob.Status = PipelineJobStatus.Failed;
            }
            else
            {
                await ApplyTextServices(completionMessage.JobId, stagedManifest, cancellationToken);
                pipelineJob.Status = PipelineJobStatus.Completed;
            }

            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;

            await manifestStorageManager.SaveManifestInStorage(stagedManifest, dbManifest, null,
                saveToStaging: false, cancellationToken);
            await iiifS3.DeleteIIIFFromS3(dbManifest, true);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error completing text pipeline for job {JobId}", completionMessage.JobId);
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Text pipeline completed for job:{JobId}, manifest:{ManifestId}. Elapsed:{Elapsed}ms",
            completionMessage.JobId, pipelineJob.ManifestId, sw.ElapsedMilliseconds);
        return true;
    }

    private async Task ApplyTextServices(string jobId, Manifest stagedManifest,
        CancellationToken cancellationToken)
    {
        var augmented = await textServicesClient.GetTextAugmentedManifest(jobId, cancellationToken);

        if (augmented?.Services == null || augmented.Services.Count == 0)
        {
            logger.LogDebug("No search services in text-augmented manifest for job {JobId}", jobId);
            return;
        }

        stagedManifest.Services ??= [];
        var existingIds = new HashSet<string?>(stagedManifest.Services.Select(s => s.Id));
        foreach (var service in augmented.Services)
        {
            if (existingIds.Add(service.Id))
                stagedManifest.Services.Add(service);
        }

        MergeContext(stagedManifest, augmented);

        logger.LogDebug("Added {Count} search service(s) to manifest for job {JobId}",
            augmented.Services.Count, jobId);
    }

    private static void MergeContext(Manifest target, Manifest source)
    {
        IEnumerable<string> contexts = source.Context switch
        {
            null => [],
            string str => [str],
            IEnumerable<string> enumerable => enumerable,
            _ => []
        };

        foreach (var context in contexts.Where(c => !IIIF.Presentation.Context.Presentation3Context.Equals(c)))
        {
            target.EnsureContext(context);
        }
    }

    private static int ExtractCustomerIdFromJobId(string jobId)
    {
        // jobId format: "{customerId}/iiif/{manifestId}"
        var firstSlash = jobId.IndexOf('/');
        return firstSlash > 0 && int.TryParse(jobId[..firstSlash], out var customerId) ? customerId : 0;
    }

    private static TextServiceJobCompletionMessage DeserializeMessage(QueueMessage message, ILogger logger)
    {
        try
        {
            return TextServiceJobCompletionMessage.FromQueueMessage(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not deserialize text-service completion message");
            throw;
        }
    }
}
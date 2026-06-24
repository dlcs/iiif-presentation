using System.Diagnostics;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Search.V2;
using Microsoft.EntityFrameworkCore;
using Models.Database.General;
using Repository;
using Repository.Helpers;
using Services.Manifests.AWS;
using Services.Manifests.Helpers;
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
    private const string Search2Context = "http://iiif.io/api/search/2/context.json";

    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(TextServiceJobCompletionMessageHandler), message.MessageId))
        {
            try
            {
                var completionMessage = DeserializeMessage(message, logger);
                var customerId = ExtractCustomerIdFromJobId(completionMessage.JobId);
                if (customerId == null)
                {
                    logger.LogWarning("Could not parse customer id from job id {JobId}; discarding message",
                        completionMessage.JobId);
                    return true;
                }

                customerIdProvider.SetCustomerId(customerId.Value);
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
        var resourceId = ExtractResourceIdFromJobId(completionMessage.JobId);
        if (resourceId == null)
        {
            logger.LogWarning("Could not parse resource id from job id {JobId}; discarding message",
                completionMessage.JobId);
            return true;
        }

        var pipelineJob = await dbContext.PipelineJobs
            .Where(p => p.ResourceId == resourceId && p.JobType == PipelineJobType.TextService)
            .OrderByDescending(p => p.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (pipelineJob == null)
        {
            var discard = approximateReceiveCount >= 2;
            logger.LogTrace(
                "PipelineJob for {JobId} not found. ApproximateReceiveCount:{Count}. {Action}",
                completionMessage.JobId, approximateReceiveCount, discard ? "Discarding" : "Will retry");
            return discard;
        }

        if (pipelineJob.Finished != null)
            logger.LogWarning("PipelineJob for {JobId} already finished at {Finished}; re-processing",
                completionMessage.JobId, pipelineJob.Finished);

        var dbManifest = await dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .SingleOrDefaultAsync(m => m.Id == pipelineJob.ResourceId && m.CustomerId == pipelineJob.CustomerId,
                cancellationToken);

        if (dbManifest == null)
        {
            logger.LogError("Manifest {ResourceId} for pipeline job {JobId} not found",
                pipelineJob.ResourceId, completionMessage.JobId);
            return false;
        }

        logger.LogInformation(
            "Completing text pipeline for job:{JobId}, customer:{CustomerId}, manifest:{ManifestId}",
            completionMessage.JobId, pipelineJob.CustomerId, pipelineJob.ResourceId);

        if (!completionMessage.IsCompleted)
        {
            logger.LogWarning("Text-services job {JobId} failed: {Errors}", completionMessage.JobId,
                completionMessage.Errors);
            pipelineJob.Error = completionMessage.Errors;
            pipelineJob.Status = PipelineJobStatus.Failed;
            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
            await iiifS3.DeleteIIIFFromS3(dbManifest, true);
            return true;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var stagedManifest =
                await iiifS3.ReadIIIFFromS3<Manifest>(dbManifest, BucketLocationType.Staging, cancellationToken);

            if (stagedManifest == null)
            {
                logger.LogError("Staged manifest not found for {ManifestId}; cannot complete text pipeline", dbManifest.Id);
                return false;
            }

            await ApplyTextServices(completionMessage.JobId, stagedManifest, cancellationToken);
            pipelineJob.Status = PipelineJobStatus.Completed;
            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;

            await manifestStorageManager.SaveManifestInStorage(stagedManifest, dbManifest, null,
                saveToStaging: false, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await iiifS3.DeleteIIIFFromS3(dbManifest, true);
            logger.LogInformation(
                "Text pipeline completed for job:{JobId}, manifest:{ManifestId}. Elapsed:{Elapsed}ms",
                completionMessage.JobId, pipelineJob.ResourceId, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error completing text pipeline for job {JobId}", completionMessage.JobId);
            return false;
        }

        return true;
    }

    private async Task ApplyTextServices(string jobId, Manifest stagedManifest,
        CancellationToken cancellationToken)
    {
        var augmented = await textServicesClient.GetTextAugmentedManifest(jobId, cancellationToken);

        if (augmented?.Service == null || !augmented.Service.OfType<SearchService2>().Any())
        {
            logger.LogDebug("No search services in text-augmented manifest for job {JobId}", jobId);
            return;
        }

        stagedManifest.Service ??= [];
        var existingIds = stagedManifest.Service.GetDistinctIds();
fixing tests
        foreach (var service in augmented.Service.OfType<SearchService2>())
        {
            if (existingIds.Add(service.Id!)) stagedManifest.Service.Add(service);
        }
        
        MergeContext(stagedManifest, augmented);

        logger.LogDebug("Added search service to manifest for job {JobId}", jobId);
    }

    private static void MergeContext(Manifest target, Manifest source)
    {
        foreach (var context in source.GetContextStrings().Where(c => c == Search2Context))
        {
            target.EnsureContext(context);
        }
    }

    private static int? ExtractCustomerIdFromJobId(string jobId)
    {
        // jobId format: "{customerId}/iiif/{resourceId}"
        var firstSlash = jobId.IndexOf('/');
        return firstSlash > 0 && int.TryParse(jobId[..firstSlash], out var customerId) ? customerId : null;
    }

    private static string? ExtractResourceIdFromJobId(string jobId)
    {
        // jobId format: "{customerId}/iiif/{resourceId}"
        var firstSlash = jobId.IndexOf('/');
        if (firstSlash < 0) return null;
        var secondSlash = jobId.IndexOf('/', firstSlash + 1);
        return secondSlash > 0 && secondSlash < jobId.Length - 1 ? jobId[(secondSlash + 1)..] : null;
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

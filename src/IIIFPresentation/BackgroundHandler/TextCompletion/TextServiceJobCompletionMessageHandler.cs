using System.Diagnostics;
using AWS.Helpers;
using AWS.SQS;
using BackgroundHandler.Helpers;
using Core.Helpers;
using Core.IIIF;
using IIIF;
using IIIF.Presentation.V3;
using IIIF.Presentation.V3.Strings;
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
    public async Task<bool> HandleMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        using (LogContextHelpers.SetServiceName(nameof(TextServiceJobCompletionMessageHandler), message.MessageId))
        {
            try
            {
                var completionMessage = DeserializeMessage(message, logger);
                var (customerId, _) = PipelineJobX.ParseJobId(completionMessage.JobId);
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
        var (customerId, resourceId) = PipelineJobX.ParseJobId(completionMessage.JobId);
        if (resourceId == null)
        {
            logger.LogWarning("Could not parse resource id from job id {JobId}; discarding message",
                completionMessage.JobId);
            return true;
        }

        var pipelineJob = await dbContext.PipelineJobs
            .Where(p => p.ManifestId == resourceId && p.JobType == PipelineJobType.TextService)
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

        if (pipelineJob.Status == PipelineJobStatus.Completed && pipelineJob.Finished != null)
        {
            logger.LogWarning("PipelineJob for {JobId} already completed at {Finished}; acknowledging",
                completionMessage.JobId, pipelineJob.Finished);
            return true;
        }

        var dbManifest = await dbContext.Manifests
            .Include(m => m.CanvasPaintings)
            .SingleOrDefaultAsync(m => m.Id == pipelineJob.ManifestId && m.CustomerId == pipelineJob.CustomerId,
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

        var searchServices = augmented?.Service?.OfType<SearchService2>().ToList();
        if (searchServices.IsNullOrEmpty())
        {
            logger.LogDebug("No search services in text-augmented manifest for job {JobId}", jobId);
            return;
        }
        
        // Add search service to manifest, if added then ensure Manifest has the search context
        stagedManifest.Service ??= [];
        var added = stagedManifest.Service.AddDistinctById(searchServices, AddService);
        if (added > 0) stagedManifest.EnsureContext(SearchService2.Search2Context);
        logger.LogDebug("Added search service to manifest for job {JobId}", jobId);
    }

    private static void AddService(IService service)
    {
        // Expectation is we'll get a SearchService2 containing an AutoCompleteService2. Set labels on these if null
        if (service is SearchService2 searchService)
        {
            searchService.Label ??= new LanguageMap("en", "Search within this manifest");
            // We're only expecting 1 here but use FirstOrDefault, rather than SingleOrDefault to avoid throwing if
            // text-service adds unexpected service. 
            var autoComplete = searchService.Service?.OfType<AutoCompleteService2>().FirstOrDefault();
            if (autoComplete != null)
            {
                autoComplete.Label ??= new LanguageMap("en", "Autocomplete words in this manifest");
            }
        }
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

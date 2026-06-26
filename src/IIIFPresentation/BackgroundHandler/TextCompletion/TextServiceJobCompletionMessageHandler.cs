using System.Diagnostics;
using AWS.SQS;
using BackgroundHandler.Infrastructure;
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
using Services.TextServices;

namespace BackgroundHandler.TextCompletion;

public class TextServiceJobCompletionMessageHandler(
    PresentationContext dbContext,
    ICustomerIdProvider customerIdProvider,
    IManifestStorageManager manifestStorageManager,
    ITextSearchClient textServicesClient,
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
        var pipelineJob = await dbContext.PipelineJobs
            .Where(p => p.ManifestId == jobId.ResourceId && p.JobType == PipelineJobType.TextService)
            .Include(p => p.Manifest)
            .OrderByDescending(p => p.Created)
            .FirstOrDefaultAsync(cancellationToken);

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

            await ApplyTextServices(jobId, staged.Manifest, cancellationToken);
            pipelineJob.Status = PipelineJobStatus.Completed;
            pipelineJob.Finished = completionMessage.Finished?.UtcDateTime;

            await manifestStorageManager.SaveManifestInStorage(staged.Manifest, dbManifest, staged.Original,
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

    private async Task ApplyTextServices(TextJobId jobId, Manifest stagedManifest, CancellationToken cancellationToken)
    {
        var augmented = await textServicesClient.GetTextAugmentedManifest(jobId, cancellationToken);

        var searchServices = augmented?.Service?.OfType<SearchService2>().ToList();
        if (searchServices.IsNullOrEmpty())
        {
            Logger.LogDebug("No SearchService2 in text-augmented manifest for job {JobId}", jobId);
            return;
        }
        
        // Add search service to manifest, if added then ensure Manifest has the search context
        stagedManifest.Service ??= [];
        var added = stagedManifest.Service.AddDistinctById(searchServices, AddService);
        if (added > 0) stagedManifest.EnsureContext(SearchService2.Search2Context);
        Logger.LogDebug("Added SearchService2 to manifest for job {JobId}", jobId);
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
}

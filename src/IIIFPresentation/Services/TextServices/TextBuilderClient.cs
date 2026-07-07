using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AWS.Helpers;
using AWS.Settings;
using Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Database.General;
using DbManifest = Models.Database.Collections.Manifest;

namespace Services.TextServices;

public class TextBuilderClient(
    HttpClient httpClient,
    IOptions<TextServicesSettings> options,
    IOptions<AWSSettings> awsOptions,
    ILogger<TextBuilderClient> logger) : ITextBuilderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<bool> UpsertJob(DbManifest manifest, PipelineJob job, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var jobId = job.GetJobId();
        if (settings.BuilderApiUri == null)
        {
            logger.LogWarning("TextServices BuilderApiUri is not configured; skipping job creation for {JobId}", jobId);
            job.Status = PipelineJobStatus.FailedToSubmit;
            return false;
        }

        var serialisedBody = CreateJobRequestJsonBody(manifest, jobId);

        var postUri = new Uri(settings.BuilderApiUri, "textbuilder");
        try
        {
            var response = await httpClient.PostAsync(postUri, GetStringContent(), cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogDebug("Text-services job {JobId} already exists, reprocessing", jobId);
                var putUri = new Uri(settings.BuilderApiUri, $"textbuilder/{jobId}");
                response = await httpClient.PutAsync(putUri, GetStringContent(), cancellationToken);
            }

        if (response.IsSuccessStatusCode)
        {
            logger.LogDebug("Text-services job {JobId} enqueued successfully", jobId);
            job.Status = PipelineJobStatus.Waiting;
            await SetInvocationCountFromResponse(response, job, jobId, cancellationToken);
            return true;
        }

            logger.LogError("Failed to create/update text-services job {JobId}: {StatusCode}", jobId,
                response.StatusCode);
        }
        catch (TaskCanceledException e)
        {
            logger.LogError(e, "Text-services job {JobId} timed out", jobId);
        }
        
        job.Status = PipelineJobStatus.FailedToSubmit;
        return false;

        StringContent GetStringContent()
        {
            return new StringContent(serialisedBody, Encoding.UTF8, "application/json");
        }
    }

    // text-services owns InvocationCount (1 on initial creation, incremented on every reprocess) - read it
    // back from the response rather than guessing locally, so it always matches what text-services will
    // later echo in its completion notification for this exact submission.
    private async Task SetInvocationCountFromResponse(HttpResponseMessage response, PipelineJob job, TextJobId jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await JsonSerializer.DeserializeAsync<TextBuilderJobResponse>(
                await response.Content.ReadAsStreamAsync(cancellationToken), JsonOptions, cancellationToken);
            if (body != null) job.InvocationCount = body.InvocationCount;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read text-services response for job {JobId}", jobId);
        }
    }

    private class TextBuilderJobResponse
    {
        public int InvocationCount { get; set; } = 1;
    }

    private string CreateJobRequestJsonBody(DbManifest manifest, TextJobId jobId)
    {
        var sourceS3Uri = GetManifestS3Key(manifest);
        var body = new { id = jobId.ToString(), sourceUri = sourceS3Uri, services = (int)JobServices.All };
        var serialisedBody = JsonSerializer.Serialize(body, JsonOptions);
        return serialisedBody;
    }

    private string GetManifestS3Key(DbManifest manifest)
    {
        var bucket = awsOptions.Value.S3.StorageBucket;
        var resourceKey = manifest.GetResourceBucketKey(BucketLocationType.Staging);
        var sourceS3Uri = $"s3://{bucket}/{resourceKey}";
        return sourceS3Uri;
    }
}

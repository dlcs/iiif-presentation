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
using Services.TextServices.Http;
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
            job.Error = "TextServices BuilderApiUri is not configured";
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
                var body = await ReadResponseBody(response, jobId, cancellationToken);
                if (body != null) job.InvocationId = body.InvocationCount.ToString();
                return true;
            }

            var errorBody = await ReadResponseBody(response, jobId, cancellationToken);
            logger.LogError("Failed to create/update text-services job {JobId}: {StatusCode} {Errors}", jobId,
                response.StatusCode, errorBody?.Errors);
            job.Error = errorBody?.Errors ?? $"Text-services returned {(int)response.StatusCode}";
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
    // back from the response rather than guessing locally, so our InvocationId always matches what
    // text-services will later echo in its completion notification for this exact submission. Also carries
    // any error message text-services returned for a rejected submission.
    private async Task<TextBuilderJobResponse?> ReadResponseBody(HttpResponseMessage response, TextJobId jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<TextBuilderJobResponse>(
                await response.Content.ReadAsStreamAsync(cancellationToken), JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read text-services response for job {JobId}", jobId);
            return null;
        }
    }

    public async Task<bool> DeleteJob(TextJobId jobId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.BuilderApiUri == null)
        {
            logger.LogWarning("TextServices BuilderApiUri is not configured; skipping job deletion for {JobId}", jobId);
            return false;
        }

        var deleteUri = new Uri(settings.BuilderApiUri, $"textbuilder/{jobId}");
        try
        {
            var response = await httpClient.DeleteAsync(deleteUri, cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("Text-services job {JobId} deleted", jobId);
                return true;
            }

            logger.LogError("Failed to delete text-services job {JobId}: {StatusCode}", jobId, response.StatusCode);
        }
        catch (TaskCanceledException e)
        {
            logger.LogError(e, "Text-services job {JobId} deletion timed out", jobId);
        }

        return false;
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

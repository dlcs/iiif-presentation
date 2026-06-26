using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Database.General;

namespace Services.TextServices;

public class TextBuilderClient(
    HttpClient httpClient,
    IOptions<TextServicesSettings> options,
    ILogger<TextBuilderClient> logger) : ITextBuilderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<bool> CreateOrUpdateJob(PipelineJob job, string bucket, string resourceKey,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var jobId = job.GetJobId();
        if (settings.BuilderApiUri == null)
        {
            logger.LogWarning("TextServices BuilderApiUri is not configured; skipping job creation for {JobId}", jobId);
            return false;
        }

        var sourceS3Uri = $"s3://{bucket}/{resourceKey}";
        var body = new { id = jobId, sourceUri = sourceS3Uri, services = (int)JobServices.All };
        var serialisedBody = JsonSerializer.Serialize(body, JsonOptions);

        var postUri = new Uri(settings.BuilderApiUri, "textbuilder");
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
            return true;
        }

        logger.LogError("Failed to create/update text-services job {JobId}: {StatusCode}", jobId, response.StatusCode);
        return false;

        StringContent GetStringContent()
        {
            return new StringContent(serialisedBody, Encoding.UTF8, "application/json");
        }
    }
}

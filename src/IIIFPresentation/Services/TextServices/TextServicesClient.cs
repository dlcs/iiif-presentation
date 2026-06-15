using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Settings;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Database.General;

namespace Services.TextServices;

public class TextServicesClient(
    HttpClient httpClient,
    IOptions<TextServicesSettings> options,
    ILogger<TextServicesClient> logger) : ITextServicesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Search=1, Autocomplete=2, TextAugmented=16
    private const int InitialServices = 19;

    public async Task<bool> CreateOrUpdateJob(PipelineJob job, string bucket, string resourceKey,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var jobId = job.GetJobId();
        if (settings.BuilderApiUri == null)
        {
            logger.LogWarning("TextServices BuilderApiUri is not configured; skipping job creation for {JobId}", jobId);
            return false;
        }

        var sourceS3Uri = $"s3://{bucket}/{resourceKey}";
        var request = new { id = jobId, sourceUri = sourceS3Uri, services = InitialServices };
        var content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");

        var postUri = new Uri(settings.BuilderApiUri, "textbuilder");
        var response = await httpClient.PostAsync(postUri, content, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogDebug("Text-services job {JobId} already exists, reprocessing", jobId);
            var putUri = new Uri(settings.BuilderApiUri, $"textbuilder/{Uri.EscapeDataString(jobId)}");
            response = await httpClient.PutAsync(putUri, null, cancellationToken);
        }

        if (response.IsSuccessStatusCode)
        {
            logger.LogDebug("Text-services job {JobId} enqueued successfully", jobId);
            return true;
        }

        logger.LogError("Failed to create/update text-services job {JobId}: {StatusCode}", jobId, response.StatusCode);
        return false;
    }

    public async Task<Manifest?> GetTextAugmentedManifest(string jobId,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (settings.SearchApiUri == null)
        {
            logger.LogWarning("TextServices SearchApiUri is not configured; cannot retrieve augmented manifest for {JobId}", jobId);
            return null;
        }

        var uri = new Uri(settings.SearchApiUri, $"text-augmented/v3/{jobId}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrEmpty(settings.CustomerOrchestratorUri))
            request.Headers.TryAddWithoutValidation("X-Forwarded-Host", settings.CustomerOrchestratorUri);
        if (!string.IsNullOrEmpty(settings.PathRules))
            request.Headers.TryAddWithoutValidation("X-Forwarded-Path", settings.PathRules);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogDebug("No text-augmented manifest found for job {JobId}", jobId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to retrieve text-augmented manifest for job {JobId}: {StatusCode}", jobId, response.StatusCode);
            return null;
        }

        return (await response.Content.ReadAsStreamAsync(cancellationToken)).FromJsonStream<Manifest>();
    }
}

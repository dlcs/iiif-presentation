using System.Net;
using Core.Settings;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services.TextServices;

public class TextSearchClient(
    HttpClient httpClient,
    IOptions<TextServicesSettings> options,
    ILogger<TextSearchClient> logger) : ITextSearchClient
{
    public async Task<Manifest?> GetTextAugmentedManifest(TextJobId jobId,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.SearchApiUri == null)
        {
            logger.LogWarning("TextServices SearchApiUri is not configured; cannot retrieve augmented manifest for {JobId}", jobId);
            return null;
        }

        var uri = new Uri(settings.SearchApiUri, $"text-augmented/v3/{jobId.ToString()}");

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

using System.Net;
using Core.Paths;
using Core.Settings;
using Core.Web;
using DLCS;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repository.Paths;

namespace Services.TextServices;

public class TextSearchClient(
    HttpClient httpClient,
    IOptions<TextServicesSettings> textServicesOptions,
    IOptions<TypedPathTemplateOptions> pathOptions,
    IOptions<DlcsSettings> dlcsOptions,
    ILogger<TextSearchClient> logger) : ITextSearchClient
{
    public async Task<Manifest?> GetTextAugmentedManifest(TextJobId jobId, CancellationToken cancellationToken)
    {
        if (textServicesOptions.Value.SearchApiUri == null)
        {
            logger.LogWarning("TextServices SearchApiUri is not configured; cannot retrieve augmented manifest for {JobId}", jobId);
            return null;
        }

        var response = await GetTextAugmentedManifestResponse(jobId, cancellationToken);

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

    /// <summary>
    /// Get HttpResponse for text-augmented manifest. We always fetch the Manifest on the same host + path but use
    /// X-Forwarded-* headers to control path format generation
    /// </summary>
    private async Task<HttpResponseMessage> GetTextAugmentedManifestResponse(TextJobId jobId, CancellationToken ct)
    {
        var settings = textServicesOptions.Value;
        const string pathPrefix = "/text-augmented/v3";
        var uri = new Uri(settings.SearchApiUri!, $"{pathPrefix}/{jobId}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        
        var orchestratorHost = dlcsOptions.Value.GetOrchestratorUri(jobId.CustomerId).Host;
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", orchestratorHost);

        var forwardedJobId = GetForwardedJobId(jobId, orchestratorHost, pathPrefix);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Path", forwardedJobId);

        logger.LogDebug("Retrieving text-augmented manifest for host {Orchestrator} using id {JobId}", orchestratorHost,
            forwardedJobId);

        var response = await httpClient.SendAsync(request, ct);
        return response;
    }

    private string GetForwardedJobId(TextJobId jobId, string orchestratorHost, string pathPrefix)
    {
        var textServiceFormat =
            pathOptions.Value.GetPathTemplateForHostAndType(orchestratorHost, PresentationResourceType.TextServiceJob);
        var newJobId = textServiceFormat.GeneratePath(jobId.CustomerId, resourceId: jobId.ResourceId);
        return $"{pathPrefix}{newJobId}";
    }
}

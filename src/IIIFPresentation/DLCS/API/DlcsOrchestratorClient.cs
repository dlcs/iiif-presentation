using IIIF.Presentation.V3;
using Microsoft.Extensions.Options;

namespace DLCS.API;

public interface IDlcsOrchestratorClient
{
    /// <summary>
    /// Retrieves a DLCS generated manifest containing assets in a given manifest
    /// </summary>
    public Task<Manifest?> RetrieveAssetsForManifest(int customerId, string manifestId,
        CancellationToken cancellationToken = default);
}

public class DlcsOrchestratorClient(
    HttpClient httpClient,
    IOptions<DlcsSettings> dlcsOptions) : IDlcsOrchestratorClient
{
    private readonly DlcsSettings settings = dlcsOptions.Value;

    public async Task<Manifest?> RetrieveAssetsForManifest(int customerId, string manifestId,
        CancellationToken cancellationToken = default)
    {
        var hostname = settings.GetOrchestratorUri(customerId);

        var uriBuilder = new UriBuilder(hostname)
        {
            Path = $"/iiif-resource/v3/{customerId}/{settings.ManifestNamedQueryName}/{manifestId}",
            Query = $"cacheBust={DateTime.UtcNow.Ticks}"
        };
        
        var response = await httpClient.GetAsync(uriBuilder.Uri, cancellationToken);
        return await response.ReadAsIIIFResponse<Manifest>(cancellationToken);
    }
}

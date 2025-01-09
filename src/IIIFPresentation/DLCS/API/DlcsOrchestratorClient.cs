using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.API;

public interface IDlcsOrchestratorClient
{
    /// <summary>
    /// Retrieves a DLCS generated manifest of images for a given presentation manifest id
    /// </summary>
    public Task<Manifest?> RetrieveImagesForManifest(int customerId, string manifestId,
        CancellationToken cancellationToken = default);
}

public class DlcsOrchestratorClient(
    HttpClient httpClient,
    IOptions<DlcsSettings> dlcsOptions,
    ILogger<DlcsOrchestratorClient> logger) : IDlcsOrchestratorClient
{
    DlcsSettings settings = dlcsOptions.Value;

    public async Task<Manifest?> RetrieveImagesForManifest(int customerId, string manifestId,
        CancellationToken cancellationToken = default)
    {
        logger.LogTrace(
            "performing a call to retrieve images for customer {CustomerId} using the manifest {ManifestId}",
            customerId, manifestId);

        var response =
            await httpClient.GetAsync($"/iiif-resource/{customerId}/{settings.ManifestNamedQueryName}/{manifestId}",
                cancellationToken);
        return await response.ReadAsIiifResponse<Manifest>(cancellationToken);
    }
}

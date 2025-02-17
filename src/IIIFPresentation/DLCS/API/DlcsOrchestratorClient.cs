using IIIF.Presentation.V3;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.API;

public interface IDlcsOrchestratorClient
{
    /// <summary>
    /// Retrieves a DLCS generated manifest containing assets in given batch ids
    /// </summary>
    public Task<Manifest?> RetrieveAssetsForManifest(int customerId, List<int> batches,
        CancellationToken cancellationToken = default);
}

public class DlcsOrchestratorClient(
    HttpClient httpClient,
    IOptions<DlcsSettings> dlcsOptions) : IDlcsOrchestratorClient
{
    private readonly DlcsSettings settings = dlcsOptions.Value;

    public async Task<Manifest?> RetrieveAssetsForManifest(int customerId, List<int> batches,
        CancellationToken cancellationToken = default)
    {
        var batchString = string.Join(',', batches);

        var requestUri =
            $"/iiif-resource/v3/{customerId}/{settings.ManifestNamedQueryName}/{batchString}?cacheBust={DateTime.UtcNow.Ticks}";
        
        var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await response.ReadAsIIIFResponse<Manifest>(cancellationToken);
    }
}

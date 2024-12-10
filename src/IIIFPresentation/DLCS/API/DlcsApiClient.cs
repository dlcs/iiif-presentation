using System.Net;
using DLCS.Exceptions;
using DLCS.Handlers;
using DLCS.Models;
using Microsoft.Extensions.Logging;

namespace DLCS.API;

public interface IDlcsApiClient
{
    /// <summary>
    /// Tests to see if the current HTTP request context is authenticated for specified customer 
    /// </summary>
    Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new space with given name for customer
    /// </summary>
    Task<Space> CreateSpace(int customerId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest assets into the DLCS
    /// </summary>
    public Task<Batch> IngestAssets<T>(int customerId, HydraCollection<T> images,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="IDlcsApiClient"/>
/// </summary>
/// <remarks>Note that this required <see cref="AmbientAuthHandler"/> to work</remarks>
internal class DlcsApiClient(
    HttpClient httpClient,
    ILogger<DlcsApiClient> logger) : IDlcsApiClient
{
    public async Task<bool> IsRequestAuthenticated(int customerId, CancellationToken cancellationToken = default)
    {
        var customerPath = $"/customers/{customerId}";
        var response = await httpClient.GetAsync(customerPath, cancellationToken);

        // if DLCS returns 200 then credentials have access
        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<Space> CreateSpace(int customerId, string name, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Creating new space for customer {CustomerId}, {SpaceName}", customerId, name);
        var spacePath = $"/customers/{customerId}/spaces";
        var payload = new Space { Name = name };
        
        var space = await CallDlcsApi<Space>(HttpMethod.Post, spacePath, payload, cancellationToken);
        return space ?? throw new DlcsException("Failed to create space");
    }
    
    public async Task<Batch> IngestAssets<T>(int customerId, HydraCollection<T> images, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Creating new batch for customer {CustomerId} with {NumberOfImages} images", customerId,
            images.Members.Count);
        var queuePath = $"/customers/{customerId}/queue";
        
        var batch = await CallDlcsApi<Batch>(HttpMethod.Post, queuePath, images, cancellationToken);
        return batch ?? throw new DlcsException("Failed to create batch");
    }

    private async Task<T?> CallDlcsApi<T>(HttpMethod httpMethod, string path, object payload,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(httpMethod, path);
        request.Content = DlcsHttpContent.GenerateJsonContent(payload);
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        return await response.ReadAsDlcsResponse<T>(cancellationToken);
    }
}

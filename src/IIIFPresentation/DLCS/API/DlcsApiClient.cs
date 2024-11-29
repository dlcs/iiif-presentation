using System.Net;
using DLCS.Handlers;
using Microsoft.Extensions.Logging;

namespace DLCS.API;

public interface IDlcsApiClient
{
    /// <summary>
    /// Tests to see if the current HTTP request context is authenticated for specified customer 
    /// </summary>
    Task<bool> IsRequestAuthenticated(int customerId);
}

/// <summary>
/// Implementation of <see cref="IDlcsApiClient"/>
/// </summary>
/// <remarks>Note that this required <see cref="AmbientAuthHandler"/> to work</remarks>
internal class DlcsApiClient(
    HttpClient httpClient,
    ILogger<DlcsApiClient> logger) : IDlcsApiClient
{
    public async Task<bool> IsRequestAuthenticated(int customerId)
    {
        var customerPath = $"/customers/{customerId}";
        var response = await httpClient.GetAsync(customerPath);

        // if DLCS returns 200 then credentials have access
        return response.StatusCode == HttpStatusCode.OK;
    }
}
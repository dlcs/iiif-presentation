using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using API.Settings;
using LazyCache;
using Microsoft.Extensions.Options;

namespace API.Auth;

/// <summary>
/// Validate that provided request contains valid auth credentials by proxying to downstream DLCS instance.
/// Any auth header provided here will be proxied to DLCS API - a 200 response = auth successful.
/// </summary>
/// <remarks>This is temporary and will be replaced in the future by an implementation that has auth logic</remarks>
public class DelegatedAuthenticator(
    HttpClient httpClient,
    IOptionsMonitor<CacheSettings> cacheSettings,
    IAppCache appCache,
    ILogger<DelegatedAuthenticator> logger) : IAuthenticator
{
    private const string AuthHeader = "Authorization";
    private const string CustomerIdRouteValue = "customerId";

    public async Task<AuthResult> ValidateRequest(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AuthHeader, out var value))
        {
            // Authorization header not in request
            return AuthResult.NoCredentials;
        }

        if (!AuthenticationHeaderValue.TryParse(value, out AuthenticationHeaderValue? headerValue)
            || string.IsNullOrEmpty(headerValue.Parameter))
        {
            // Invalid Authorization header
            return AuthResult.NoCredentials;
        }

        if (!request.RouteValues.TryGetValue(CustomerIdRouteValue, out var customerIdRouteVal)
            || customerIdRouteVal is null)
        {
            logger.LogDebug("Unable to identify customerId in auth request to {request}", request.Path);
            return AuthResult.NoCredentials;
        }

        var customerId = customerIdRouteVal.ToString()!;
        return await IsValidUser(headerValue, customerId)
            ? AuthResult.Success
            : AuthResult.Failed;
    }

    private async Task<bool> IsValidUser(AuthenticationHeaderValue authenticationHeaderValue, string customerId)
    {
        var cacheKey = $"{customerId}:{authenticationHeaderValue.Scheme}";
        var authParameter = authenticationHeaderValue.Parameter!;
        
        var list = await appCache.GetAsync<ConcurrentBag<string>>(cacheKey);
        if (list != null && list.Contains(authParameter)) return true;

        var isValid = await IsValidUserDlcs(authenticationHeaderValue, customerId);
        if (!isValid) return false;
        
        list ??= [];
        list.Add(authParameter);
        appCache.Add(cacheKey, list, cacheSettings.CurrentValue.GetMemoryCacheOptions());
        return true;
    }

    private async Task<bool> IsValidUserDlcs(AuthenticationHeaderValue authenticationHeaderValue, string customerId)
    {
        // Parse the CustomerId out of this.
        var delegatePath = $"/customers/{customerId}";

        // make a request to DLCS and verify the result received - if it's 200 we're good
        var request = new HttpRequestMessage(HttpMethod.Get, delegatePath);
        request.Headers.Authorization = authenticationHeaderValue;
        var response = await httpClient.SendAsync(request);

        return response.StatusCode == HttpStatusCode.OK;
    }
}

public enum AuthResult
{
    Success,
    Failed,
    NoCredentials,
}
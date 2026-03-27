using System.Collections.Concurrent;
using System.Net.Http.Headers;
using API.Infrastructure.Helpers;
using API.Settings;
using DLCS;
using DLCS.API;
using LazyCache;
using Microsoft.Extensions.Options;

namespace API.Auth;

/// <summary>
/// Validate that provided request contains valid auth credentials by proxying to downstream DLCS instance.
/// Any auth header provided here will be proxied to DLCS API - a 200 response = auth successful.
/// </summary>
/// <remarks>This is temporary and will be replaced in the future by an implementation that has auth logic</remarks>
public class DelegatedAuthenticator(
    IDlcsApiClient dlcsApiClient,
    IOptionsMonitor<CacheSettings> cacheSettings,
    IAppCache appCache,
    ILogger<DelegatedAuthenticator> logger) : IAuthenticator
{
    public async Task<AuthResult> ValidateRequest(HttpRequest request, CancellationToken cancellationToken = default)
    {
        var headerValue = request.TryGetValidAuthHeader();
        
        if (headerValue == null)
        {
            // Missing or invalid Authorization header
            return AuthResult.NoCredentials;
        }

        var customerId = request.GetCustomerId(logger);
        if (!customerId.HasValue) return AuthResult.NoCredentials;

        return await IsValidUser(headerValue, customerId.Value, cancellationToken)
            ? AuthResult.Success
            : AuthResult.Failed;
    }

    private async Task<bool> IsValidUser(AuthenticationHeaderValue authenticationHeaderValue, int customerId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{customerId}:{authenticationHeaderValue.Scheme}";
        var authParameter = authenticationHeaderValue.Parameter!;
        
        var list = await appCache.GetAsync<ConcurrentBag<string>>(cacheKey);
        if (list != null && list.Contains(authParameter)) return true;

        var isValid = await dlcsApiClient.IsRequestAuthenticated(customerId, cancellationToken);
        if (!isValid) return false;
        
        list ??= [];
        list.Add(authParameter);
        appCache.Add(cacheKey, list, cacheSettings.CurrentValue.GetMemoryCacheOptions());
        return true;
    }
}

public enum AuthResult
{
    Success,
    Failed,
    NoCredentials,
}

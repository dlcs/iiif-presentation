using System.Net.Http.Headers;
using API.Auth;
using Microsoft.AspNetCore.Http;

namespace API.Tests.Integration.Infrastructure;

/// <summary>
/// Test-only <see cref="IAuthenticator"/> which will pass auth as long as Auth header present
/// </summary>
public class TestAuthenticator : IAuthenticator
{
    private const string AuthHeader = "Authorization";
    
    public async Task<AuthResult> ValidateRequest(HttpRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Headers.TryGetValue(AuthHeader, out var value))
        {
            // Authorization header not in request
            return AuthResult.NoCredentials;
        }
        
        if (!AuthenticationHeaderValue.TryParse(value, out _))
        {
            // Invalid Authorization header
            return AuthResult.NoCredentials;
        }
        
        return AuthResult.Success;
    }
}
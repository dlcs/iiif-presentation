using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Test.Helpers.Integration;

public static class TestAuthHandlerX
{
    public static HttpClient AsCustomer(this HttpClient client, int customer = 2)
    {
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue($"user|{customer}");
        return client;
    }
}

/// <summary>
/// Authentication Handler to make testing easier.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string AuthHeader = "Authorization";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthHeader, out var value))
        {
            // Authorization header not in request
            return AuthenticateResult.NoResult();
        }
        
        if (!AuthenticationHeaderValue.TryParse(value, out _))
        {
            // Invalid Authorization header
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ClaimsIssuer);
        var result = AuthenticateResult.Success(ticket);
        return result;
    }
}
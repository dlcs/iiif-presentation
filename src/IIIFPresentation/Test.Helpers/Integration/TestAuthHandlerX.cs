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

using System.Net.Http.Headers;

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
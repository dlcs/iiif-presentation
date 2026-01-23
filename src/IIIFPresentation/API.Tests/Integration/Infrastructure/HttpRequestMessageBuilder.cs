using System.Net.Http.Headers;
using Test.Helpers;

namespace API.Tests.Integration.Infrastructure;

public static class HttpRequestMessageBuilder
{
    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path, string content,
        Guid? etag = null)
    {
        var requestMessage = new HttpRequestMessage(method, path).WithJsonContent(content);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        if (etag is not null)
            requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{etag:N}\""));

        return requestMessage;
    }

    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path, Guid? etag = null)
    {
        var requestMessage = new HttpRequestMessage(method, path);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        if (etag is not null)
            requestMessage.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{etag:N}\""));

        return requestMessage;
    }

    public static void AddHostExampleHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "example.com");
    }

    public static void AddHostNoCustomerHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "no-customer.com");
    }
    
    public static void AddHostNoCustomerAdditionalPathElementHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "no-customer-additional-path-element.com");
    }
    
    public static void AddHostNoCustomerMultiplePathElementHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "no-customer-multiple-path-element.com");
    }
    
    public static void AddHostFullyQualifiedCustomerHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "fully-qualified.com");
    }
    
    public static void AddHostFullyQualifiedNoCustomerHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "fully-qualified-no-customer.com");
    }
    
    public static void AddLinkHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Link", "<https://dlcs.io/vocab#Space>;rel=\"DCTERMS.requires\"");
    }
}

using Test.Helpers;

namespace API.Tests.Integration.Infrastructure;

public static class HttpRequestMessageBuilder
{
    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path, string content)
    {
        var requestMessage = new HttpRequestMessage(method, path).WithJsonContent(content);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        
        return requestMessage;
    }
    
    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path)
    {
        var requestMessage = new HttpRequestMessage(method, path);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        
        return requestMessage;
    }
    
    public static void AddPathRewriteHeader(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("Host", "example.com");
    }
}

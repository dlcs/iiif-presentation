using System.Text;

namespace API.Tests.Integration.Infrastructure;

public static class HttpRequestMessageBuilder
{
    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path, string content)
    {
        var requestMessage = new HttpRequestMessage(method, path);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        requestMessage.Content = new StringContent(content, Encoding.UTF8, "application/json");
        
        return requestMessage;
    }
    
    public static HttpRequestMessage GetPrivateRequest(HttpMethod method, string path)
    {
        var requestMessage = new HttpRequestMessage(method, path);
        requestMessage.Headers.Add("X-IIIF-CS-Show-Extras", "All");
        
        return requestMessage;
    }
}
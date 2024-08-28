namespace API.Tests.Integration.Infrastucture;

public static class HttpRequestMessageX
{
    public static void AddPrivateHeaders(this HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Add("IIIF-CS-Show-Extra", "All");
    }
}
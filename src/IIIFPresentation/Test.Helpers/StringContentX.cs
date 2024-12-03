using System.Text;

namespace Test.Helpers;

public static class StringContentX
{
    public static HttpRequestMessage WithJsonContent(this HttpRequestMessage request, string content)
    {
        request.Content = new StringContent(content, Encoding.UTF8, "application/json");
        return request;
    }
}
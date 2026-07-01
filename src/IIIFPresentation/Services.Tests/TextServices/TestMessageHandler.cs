using System.Net;
using System.Text;

namespace Services.Tests.TextServices;

internal class TestMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content != null)
            response.Content = new StringContent(content, Encoding.UTF8, "application/json");
        responses.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responses.Count > 0
            ? responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK));
    }
}

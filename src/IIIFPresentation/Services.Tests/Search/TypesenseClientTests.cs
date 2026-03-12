using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Services.Search;

namespace Services.Tests.Search;

public class TypesenseClientTests
{
    [Fact]
    public async Task ImportDocumentsAsync_UsesUpsertEndpoint_AndParsesJsonlResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {"success":true}
                {"success":false,"error":"nope"}
                """,
                Encoding.UTF8,
                "text/plain")
        });

        var sut = new TypesenseClient(new HttpClient(handler) { BaseAddress = new Uri("https://typesense.example") });

        var results = await sut.ImportDocumentsAsync("search_collection", [new { id = "one" }, new { id = "two" }]);

        results.Should().HaveCount(2);
        results.Should().ContainSingle(r => r.Success);
        results.Should().ContainSingle(r => !r.Success && r.Error == "nope");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].PathAndQuery.Should().Be("/collections/search_collection/documents/import?action=upsert");
        handler.Requests[0].Body.Should().Contain("\"id\":\"one\"");
        handler.Requests[0].Body.Should().Contain("\"id\":\"two\"");
    }

    [Fact]
    public async Task DeleteDocumentAsync_ReturnsFalse_WhenTypesenseReturnsNotFound()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = new TypesenseClient(new HttpClient(handler) { BaseAddress = new Uri("https://typesense.example") });

        var deleted = await sut.DeleteDocumentAsync("search_collection", "missing-id");

        deleted.Should().BeFalse();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].PathAndQuery.Should().Be("/collections/search_collection/documents/missing-id?ignore_not_found=true");
    }

    private sealed class StubHttpMessageHandler(Func<CapturedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var captured = new CapturedRequest(request.Method, request.RequestUri!.PathAndQuery, body);
            Requests.Add(captured);
            return responder(captured);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string PathAndQuery, string Body);
}

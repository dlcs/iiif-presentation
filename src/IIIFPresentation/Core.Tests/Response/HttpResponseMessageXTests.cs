using System.Net;
using System.Net.Http.Headers;
using Core.Response;
using IIIF.Presentation.V3;
using Models.API.Collection;

namespace Core.Tests.Response;

public class HttpResponseMessageXTests
{
    [Fact]
    public async Task ReadAsPresentationJsonAsync_Throws_IfEnsureSuccessAndUnsuccessful()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway);

        Func<Task> action = () => response.ReadAsPresentationJsonAsync<Manifest>();

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ReadAsPresentationJsonAsync_ReturnsDefault_IfNonJsonResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        var actual = await response.ReadAsPresentationJsonAsync<Manifest>();

        actual.Should().BeNull();
    }
    
    [Fact]
    public async Task ReadAsPresentationJsonAsync_ReturnsDeserialized_IfErrorAndEnsureSuccessFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{\"id\": \"test-sample\"}", new MediaTypeHeaderValue("application/json")),
        };

        var actual = await response.ReadAsPresentationJsonAsync<Manifest>(ensureSuccess: false);

        actual.Id.Should().Be("test-sample");
    }
    
    [Fact]
    public async Task ReadAsPresentationJsonAsync_ReturnsDeserialized_StandardIIIFModel()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": \"test-sample\"}", new MediaTypeHeaderValue("application/json")),
        };

        var actual = await response.ReadAsPresentationJsonAsync<Collection>();

        actual.Id.Should().Be("test-sample");
    }
    
    [Fact]
    public async Task ReadAsPresentationJsonAsync_ReturnsDeserialized_NonStandardIIIFModel()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\": \"test-sample\", \"slug\": \"foo\"}", new MediaTypeHeaderValue("application/json")),
        };

        var actual = await response.ReadAsPresentationJsonAsync<PresentationCollection>();

        actual.Id.Should().Be("test-sample");
        actual.Slug.Should().Be("foo");
    }
}
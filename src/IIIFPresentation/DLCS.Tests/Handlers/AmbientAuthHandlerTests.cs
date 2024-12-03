using System.Net;
using DLCS.Handlers;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.Tests.Handlers;

public class AmbientAuthHandlerTests
{
    [Fact]
    public async Task SendAsync_Returns401_AndDoesNotMakeDownstreamCall_IfNoAuth()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5000");
        var sut = GetSut(out var testHandler);
        var invoker = new HttpMessageInvoker(sut);
        
        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        testHandler.Request.Should().BeNull();
    }

    [Theory]
    [InlineData("Basic", "abc1234")]
    [InlineData("Bearer", "abc1234")]
    public async Task SendAsync_PassesAuth_FromCurrentContext_ToDownstreamRequest(string scheme, string parameter)
    { 
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5000");
        var sut = GetSut(out var testHandler, () => new DefaultHttpContext
        {
            Request = { Headers = { ["Authorization"] = $"{scheme} {parameter}" } },
        });
        var invoker = new HttpMessageInvoker(sut);
        
        await invoker.SendAsync(request, CancellationToken.None);

        testHandler.Request.Should().Match<HttpRequestMessage>(r =>
            r.Headers.Authorization.Scheme == scheme && r.Headers.Authorization.Parameter == parameter);
    }

    private AmbientAuthHandler GetSut(out TestHandler innerHandler, Func<HttpContext>? contextFactory = null)
    {
        innerHandler = new TestHandler();
        contextFactory ??= () => new DefaultHttpContext();
        var contextAccessor = A.Fake<IHttpContextAccessor>();
        A.CallTo(() => contextAccessor.HttpContext).Returns(contextFactory());

        var sut = new AmbientAuthHandler(contextAccessor, new NullLogger<AmbientAuthHandler>());
        sut.InnerHandler = innerHandler;
        return sut;
    }
}

public class TestHandler : DelegatingHandler
{
    public HttpRequestMessage? Request { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Request = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
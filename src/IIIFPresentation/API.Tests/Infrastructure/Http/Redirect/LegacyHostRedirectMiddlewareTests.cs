using API.Infrastructure.Http.Redirect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Services.Manifests.Settings;

namespace API.Tests.Infrastructure.Http.Redirect;

public class LegacyHostRedirectMiddlewareTests
{
    private const string LegacyHost = "legacy.example.com";

    private static LegacyHostRedirectMiddleware CreateSut(RequestDelegate next, Uri presentationApiUrl) =>
        new(next, Options.Create(new PathSettings
        {
            PresentationApiUrl = presentationApiUrl,
            LegacyPresentationApiUrl = new Uri($"https://{LegacyHost}")
        }), NullLogger<LegacyHostRedirectMiddleware>.Instance);

    private static HttpContext CreateAuthorisedLegacyHostContext()
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Host = new HostString(LegacyHost),
                Path = "/1/iiif-manifest",
                Scheme = "https"
            }
        };
        context.Request.Headers.Authorization = "Bearer token";
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task InvokeAsync_Authorised_SwapsHost_WithoutDefaultPort_WhenCanonicalUrlHasNoPort()
    {
        // Arrange - regression coverage for HostString.FromUriComponent(Uri) always including the port (even the
        // scheme's default one) - downstream id/path generation reads this host verbatim, so an unwanted ":443"
        // here would end up baked into every generated path
        HostString? hostSeenDownstream = null;
        var sut = CreateSut(ctx =>
        {
            hostSeenDownstream = ctx.Request.Host;
            return Task.CompletedTask;
        }, new Uri("https://canonical.example.com"));
        var context = CreateAuthorisedLegacyHostContext();

        // Act
        await sut.InvokeAsync(context);

        // Assert
        hostSeenDownstream!.Value.Value.Should().Be("canonical.example.com");
    }

    [Fact]
    public async Task InvokeAsync_Authorised_SwapsHost_KeepingNonDefaultPort()
    {
        // Arrange - a genuinely non-default port on PresentationApiUrl must still carry through
        HostString? hostSeenDownstream = null;
        var sut = CreateSut(ctx =>
        {
            hostSeenDownstream = ctx.Request.Host;
            return Task.CompletedTask;
        }, new Uri("https://canonical.example.com:8080"));
        var context = CreateAuthorisedLegacyHostContext();

        // Act
        await sut.InvokeAsync(context);

        // Assert
        hostSeenDownstream!.Value.Value.Should().Be("canonical.example.com:8080");
    }

    [Fact]
    public async Task InvokeAsync_Authorised_RestoresOriginalHost_AfterNext()
    {
        // Arrange - the swap is only for the duration of next(); it must not leak back onto the shared HttpContext
        // once the request has finished being processed in place
        var sut = CreateSut(_ => Task.CompletedTask, new Uri("https://canonical.example.com"));
        var context = CreateAuthorisedLegacyHostContext();

        // Act
        await sut.InvokeAsync(context);

        // Assert
        context.Request.Host.Value.Should().Be(LegacyHost);
    }
}

using System.Net;
using API.Infrastructure;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using IIIF.Presentation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Models.API.Collection;
using Models.API.General;

namespace API.Tests.Infrastructure;

public class ControllerBaseXTests
{
    private readonly ControllerBase controller = GetController();

    private static ControllerBase GetController(Action<HttpRequest>? setupRequest = null)
    {
        var httpContext = new DefaultHttpContext();

        if (setupRequest == null)
        {
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("localhost");
        }
        else setupRequest(httpContext.Request);

        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    [Fact]
    public void FetchResultToHttpResult_Success_ReturnsPresentationContent()
    {
        // Arrange
        var entity = new PresentationCollection { Id = "http://localhost/1/collections/root" };
        var etag = Guid.NewGuid();

        // Act
        var result = controller.FetchResultToHttpResult(FetchEntityResult<PresentationCollection>.Success(entity, etag));

        // Assert
        var content = result.Should().BeOfType<CacheableContentResult>().Subject;
        content.StatusCode.Should().Be((int)HttpStatusCode.OK);
        content.ContentType.Should().Be(ContentTypes.V3, "the IIIF content type, not default JSON");
        content.ETag.Should().Be(etag);
        content.Content.Should().Contain("http://localhost/1/collections/root");
    }

    [Fact]
    public void FetchResultToHttpResult_Success_NoETag_ReturnsPresentationContent()
    {
        // Arrange
        var entity = new PresentationCollection { Id = "http://localhost/1/collections/root" };

        // Act
        var result = controller.FetchResultToHttpResult(FetchEntityResult<PresentationCollection>.Success(entity));

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().Be((int)HttpStatusCode.OK);
        content.ContentType.Should().Be(ContentTypes.V3);
    }

    [Fact]
    public void FetchResultToHttpResult_Matched_ReturnsNotModified()
    {
        // Arrange
        var etag = Guid.NewGuid();

        // Act
        var result = controller.FetchResultToHttpResult(FetchEntityResult<PresentationCollection>.Matched(etag));

        // Assert - NotModifiedResult holds the etag privately, so execute it to see what it emits
        var notModified = result.Should().BeOfType<NotModifiedResult>().Subject;
        notModified.ExecuteResult(new ActionContext(controller.HttpContext, new RouteData(), new ActionDescriptor()));

        controller.Response.StatusCode.Should().Be((int)HttpStatusCode.NotModified);
        controller.Response.Headers.ETag.ToString().Should().Be($"\"{etag:N}\"");
    }

    [Fact]
    public void FetchResultToHttpResult_Invalid_ReturnsBadRequest()
    {
        // Act
        var result = controller.FetchResultToHttpResult(
            FetchEntityResult<PresentationCollection>.Invalid("only storage collections"), errorTitle: "Search failed");

        // Assert
        var error = GetError(result, HttpStatusCode.BadRequest);
        error.Detail.Should().Be("only storage collections");
        error.Title.Should().Be("Search failed: Bad request");
    }

    [Fact]
    public void FetchResultToHttpResult_Failure_ReturnsInternalServerError()
    {
        // Act
        var result = controller.FetchResultToHttpResult(FetchEntityResult<PresentationCollection>.Failure("boom"));

        // Assert
        GetError(result, HttpStatusCode.InternalServerError).Detail.Should().Be("boom");
    }

    [Fact]
    public void FetchResultToHttpResult_NotFound_ReturnsNotFound()
    {
        // Act
        var result = controller.FetchResultToHttpResult(FetchEntityResult<PresentationCollection>.NotFound());

        // Assert
        GetError(result, HttpStatusCode.NotFound).Title.Should().Be("Not Found");
    }

    [Fact]
    public void PresentationProblem_Correct_WithDefaults_RequestHasPathBase()
    {
        // Setup a basic request with no query params
        var sut = GetController(request =>
        {
            request.Scheme = "https";
            request.Host = new HostString("localhost");
            request.Path = "/manifest/1234";
            request.PathBase = "/v1";
        });
        var result = sut.PresentationProblem();
        
        var error = GetError(result, HttpStatusCode.InternalServerError);
        error.Detail.Should().BeNull();
        error.Title.Should().BeNull();
        error.Status.Should().Be(500);
        error.ErrorTypeUri.Should().BeNull();
        
        // PathBase cannot be set in API, there is no configuration for it but included test as this is the behaviour using the helpers provided
        error.Instance.Should().Be("https://localhost/v1", "Instance defaults to {scheme}:{host}{pathBase}");
    }
    
    [Fact]
    public void PresentationProblem_Correct_WithDefaults_RequestHasQueryParam()
    {
        // Setup a basic request with no query params
        var sut = GetController(request =>
        {
            request.Scheme = "https";
            request.Host = new HostString("localhost");
            request.Path = "/manifest/1234";
            request.QueryString = new QueryString("?foo=bar");
        });
        var result = sut.PresentationProblem();
        
        var error = GetError(result, HttpStatusCode.InternalServerError);
        error.Detail.Should().BeNull();
        error.Title.Should().BeNull();
        error.Status.Should().Be(500);
        error.ErrorTypeUri.Should().BeNull();
        error.Instance.Should().Be("https://localhost", "Query params are not included");
    }
    
    [Fact]
    public void PresentationProblem_Correct_AllValuesProvidedValues()
    {
        // Setup a basic request with no query params
        var sut = GetController(request =>
        {
            request.Scheme = "https";
            request.Host = new HostString("localhost");
            request.Path = "/manifest/1234";
            request.QueryString = new QueryString("?foo=bar");
        });
        var result =
            sut.PresentationProblem("Details", "https://error-instance", 429, "I am title", "https://error-type");
        
        var error = GetError(result, HttpStatusCode.TooManyRequests);
        error.Detail.Should().Be("Details");
        error.Title.Should().Be("I am title");
        error.Status.Should().Be(429);
        error.ErrorTypeUri.Should().Be("https://error-type");
        error.Instance.Should().Be("https://error-instance");
    }

    private static Error GetError(IActionResult result, HttpStatusCode expectedStatus)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)expectedStatus);
        return objectResult.Value.Should().BeOfType<Error>().Subject;
    }

    private class TestController : ControllerBase;
}

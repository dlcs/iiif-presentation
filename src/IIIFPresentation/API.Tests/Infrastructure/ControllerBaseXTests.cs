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

    private static ControllerBase GetController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");

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

    private static Error GetError(IActionResult result, HttpStatusCode expectedStatus)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be((int)expectedStatus);
        return objectResult.Value.Should().BeOfType<Error>().Subject;
    }

    private class TestController : ControllerBase;
}

using System.Net;
using API.Infrastructure.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Models.API.General;

namespace API.Tests.Infrastructure.Filters;

public class RequireShowExtrasAttributeTests
{
    private readonly RequireShowExtrasAttribute sut = new();

    [Fact]
    public void OnActionExecuting_DoesNotShortCircuit_IfShowExtrasHeaderPresent()
    {
        // Arrange
        var context = GetActionExecutingContext("All");

        // Act
        sut.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeNull("action is allowed to run");
    }

    [Theory]
    [InlineData("")]
    [InlineData("What")]
    [InlineData(null)]
    public void OnActionExecuting_Returns403_IfShowExtrasHeaderMissingOrUnknown(string? showExtrasVal)
    {
        // Arrange
        var context = GetActionExecutingContext(showExtrasVal);

        // Act
        sut.OnActionExecuting(context);

        // Assert
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);

        var error = result.Value.Should().BeOfType<Error>().Subject;
        error.Status.Should().Be((int)HttpStatusCode.Forbidden);
        error.Instance.Should().Be("http://localhost/1/collections/root/search?label=medicine");
    }

    [Fact]
    public void Order_IsAfterVaryHeader_SoVaryIsEmittedOnShortCircuit()
    {
        // A short-circuiting filter skips its own OnActionExecuted, but not that of filters that
        // already ran - VaryHeaderAttribute must run first for Vary to be set on the 403
        sut.Order.Should().BeGreaterThan(new VaryHeaderAttribute().Order);
    }

    private static ActionExecutingContext GetActionExecutingContext(string? showExtrasVal)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/1/collections/root/search";
        httpContext.Request.QueryString = new QueryString("?label=medicine");

        if (showExtrasVal != null)
        {
            httpContext.Request.Headers.Append("X-IIIF-CS-Show-Extras", showExtrasVal);
        }

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor()),
            [], new Dictionary<string, object?>(), controller: null!);
    }
}

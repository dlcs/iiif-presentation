using API.Attributes;
using API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Models.Database.Collections;

namespace API.Tests.Attributes;

public class ETagCachingAttributeTests
{
    private static ActionContext CreateActionContext(HttpContext context) => new(context, new(), new());

    private static ResultExecutedContext CreateResultExecutedContext(HttpContext context) =>
        new ResultExecutedContext(CreateActionContext(context), [], new ObjectResult(null), new object());

    private static ResultExecutingContext CreateResultExecutingContext(HttpContext context) =>
        new ResultExecutingContext(CreateActionContext(context), [], new ObjectResult(null), new object());
    

    [Fact]
    // https://github.com/dlcs/iiif-presentation/pull/141
    public async Task Should_Reuse_Response_Etag()
    {
        // Arrange
        var filter = new ETagCachingAttribute();
        var services = new ServiceCollection();

        var context = CreateResultExecutingContext(new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        });

        var responseHeaders = context.HttpContext.Response.GetTypedHeaders();
        responseHeaders.ETag = new EntityTagHeaderValue("\"abc\"");

        // Act
        await filter.OnResultExecutionAsync(context, () => Task.FromResult(CreateResultExecutedContext(context.HttpContext)));

        // Assert
        Assert.Single(context.HttpContext.Response.Headers.CacheControl, "no-cache");
        Assert.Single(context.HttpContext.Response.Headers.ETag, "\"abc\"");
    }

    [Fact]
    public async Task Should_Generate_Etag_For_Small_Content()
    {
        var filter = new ETagCachingAttribute();
        var services = new ServiceCollection();

        var context = CreateResultExecutingContext(new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        });

        // Act
        await filter.OnResultExecutionAsync(context, () => Task.FromResult(CreateResultExecutedContext(context.HttpContext)));

        // Assert
        
        // This ETag is of an empty body.
        Assert.Single(context.HttpContext.Response.Headers.ETag, "\"1B2M2Y8AsgTpgAmY7PhCfg==\"");
    }
    
    [Fact(Skip = "public caching is currently disabled")]

    public async Task Should_Use_MaxAge_Cache_For_Large_Content()
    {
        // See limit in API.Attributes.ETagCachingAttribute.IsEtagSupported
        var filter = new ETagCachingAttribute();
        var services = new ServiceCollection();
        
        var context = CreateResultExecutingContext(new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        });

        // Act
        await filter.OnResultExecutionAsync(context, () =>
        {
            var largeContentThreshold = 512 * 1024;
            var stream = new MemoryStream(new byte[largeContentThreshold]);;
            stream.SetLength(largeContentThreshold);
            context.HttpContext.Response.Body = stream;
            
            return Task.FromResult(CreateResultExecutedContext(context.HttpContext));
        });

        // Assert
        Assert.Single(context.HttpContext.Response.Headers.CacheControl, "max-age=10");
    }
    
    [Fact(Skip = "public caching is currently disabled")]
    public async Task Should_Use_PublicMaxAge_Cache_For_Large_Content_With_ShowExtras()
    {
        // See limit in API.Attributes.ETagCachingAttribute.IsEtagSupported
        var filter = new ETagCachingAttribute();
        var services = new ServiceCollection();
        
        var context = CreateResultExecutingContext(new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        });

        // Act
        await filter.OnResultExecutionAsync(context, () =>
        {
            var largeContentThreshold = 512 * 1024;
            var stream = new MemoryStream(new byte[largeContentThreshold]);;
            stream.SetLength(largeContentThreshold);
            context.HttpContext.Response.Body = stream;
            context.HttpContext.Request.Headers["X-IIIF-CS-Show-Extras"] = "All";
            
            return Task.FromResult(CreateResultExecutedContext(context.HttpContext));
        });

        // Assert
        Assert.Single(context.HttpContext.Response.Headers.CacheControl, "public, max-age=10");
    }
}

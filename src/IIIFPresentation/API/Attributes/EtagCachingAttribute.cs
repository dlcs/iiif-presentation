using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace API.Attributes;

public class EtagCachingAttribute : ActionFilterAttribute
{
    // When a "304 Not Modified" response is to be sent back to the client, all headers apart from the following list should be stripped from the response to keep the response size minimal. See https://datatracker.ietf.org/doc/html/rfc7232#section-4.1:~:text=200%20(OK)%20response.-,The%20server%20generating%20a%20304,-response%20MUST%20generate
    private static readonly string[] headersToKeepFor304 =
    {
        HeaderNames.CacheControl,
        HeaderNames.ContentLocation,
        HeaderNames.ETag,
        HeaderNames.Expires,
        HeaderNames.Vary
    };

    private static readonly Dictionary<string, string> etagHashes = new();

    // Adds cache headers to response
    public override async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;

        // For more info on this technique, see https://stackoverflow.com/a/65901913 and https://www.madskristensen.net/blog/send-etag-headers-in-aspnet-core/ and https://gist.github.com/madskristensen/36357b1df9ddbfd123162cd4201124c4
        var originalStream = response.Body;
        using MemoryStream memoryStream = new();

        response.Body = memoryStream;
        await next();
        memoryStream.Position = 0;

        if (response.StatusCode == StatusCodes.Status200OK)
        {
            var requestHeaders = request.GetTypedHeaders();
            var responseHeaders = response.GetTypedHeaders();

            responseHeaders.CacheControl = new CacheControlHeaderValue() // how long clients should cache the response
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(365)
            };
            responseHeaders.ETag ??=
                GenerateETag(memoryStream,
                    request.Path); // This request generates a hash from the response - this would come from S3 in live

            if (IsClientCacheValid(requestHeaders, responseHeaders))
            {
                response.StatusCode = StatusCodes.Status304NotModified;

                // Remove all unnecessary headers while only keeping the ones that should be included in a `304` response.
                foreach (var header in response.Headers)
                    if (!headersToKeepFor304.Contains(header.Key))
                        response.Headers.Remove(header.Key);

                return;
            }
        }

        await memoryStream
            .CopyToAsync(
                originalStream); // Writes anything the later middleware wrote to the body (and by extension our `memoryStream`) to the original response body stream, so that it will be sent back to the client as the response body.
    }

    private static EntityTagHeaderValue GenerateETag(Stream stream, string path)
    {
        var hashBytes = MD5.HashData(stream);
        stream.Position = 0;
        var hashString = Convert.ToBase64String(hashBytes);

        var enityTagHeader =
            new EntityTagHeaderValue('"' + hashString +
                                     '"'); // An `ETag` needs to be surrounded by quotes. See https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/ETag#:~:text=It%20is%20a%20string%20of%20ASCII%20characters%20placed%20between%20double%20quotes

        etagHashes[path] = enityTagHeader.Tag.ToString();
        return enityTagHeader;
    }

    private static bool IsClientCacheValid(RequestHeaders reqHeaders, ResponseHeaders resHeaders)
    {
        // If both `If-None-Match` and `If-Modified-Since` are present in a request, `If-None-Match` takes precedence and `If-Modified-Since` is ignored (provided, of course, that the resource supports entity-tags, hence the second condition after the `&&` operator in the following `if`). See https://datatracker.ietf.org/doc/html/rfc7232#section-3.3:~:text=A%20recipient%20MUST%20ignore%20If%2DModified%2DSince%20if
        if (reqHeaders.IfNoneMatch.Any() && resHeaders.ETag is not null)
            return reqHeaders.IfNoneMatch.Any(etag =>
                    etag.Compare(resHeaders.ETag,
                        false) // We shouldn't use `Contains` here because it would use the `Equals` method which apparently shouldn't be used for ETag equality checks. See https://learn.microsoft.com/en-us/dotnet/api/microsoft.net.http.headers.entitytagheadervalue.equals?view=aspnetcore-7.0. We also use weak comparison, because that seems to what the built-in response caching middleware (which is general-purpose enough in this particular respect to be able to inform us here) is doing. See https://github.com/dotnet/aspnetcore/blob/7f4ee4ac2fc945eab33d004581e7b633bdceb475/src/Middleware/ResponseCaching/src/ResponseCachingMiddleware.cs#LL449C51-L449C70
            );

        if (reqHeaders.IfModifiedSince is not null && resHeaders.LastModified is not null)
            return reqHeaders.IfModifiedSince >= resHeaders.LastModified;

        return false;
    }

    // checks request for valid cache headers
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.HttpContext.Request;

        if (request.Method == HttpMethod.Put.ToString())
        {
            if (request.Headers.IfMatch.Count == 0)
                context.Result = new ObjectResult("This method requires a valid ETag to be present")
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };

            etagHashes.TryGetValue(request.Path, out var etag);

            if (!request.Headers.IfMatch.Equals(etag))
                context.Result = new ObjectResult("Cannot match ETag")
                {
                    StatusCode = StatusCodes.Status412PreconditionFailed
                };
        }

        OnActionExecuting(context);
        if (context.Result == null) OnActionExecuted(await next());
    }
}
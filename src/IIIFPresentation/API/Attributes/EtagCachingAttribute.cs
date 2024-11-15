using System.Security.Cryptography;
using API.Infrastructure.Helpers;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using Models.API.General;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using HttpMethod = System.Net.Http.HttpMethod;

namespace API.Attributes;

public class ETagCachingAttribute : ActionFilterAttribute
{
    // When a "304 Not Modified" response is to be sent back to the client, all headers apart from the following list should be stripped from the response to keep the response size minimal. See https://datatracker.ietf.org/doc/html/rfc7232#section-4.1:~:text=200%20(OK)%20response.-,The%20server%20generating%20a%20304,-response%20MUST%20generate
    private static readonly string[] HeadersToKeepFor304 =
    {
        HeaderNames.CacheControl,
        HeaderNames.ContentLocation,
        HeaderNames.ETag,
        HeaderNames.Expires,
        HeaderNames.Vary
    };

    // Adds cache headers to response
    public override async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        var request = context.HttpContext.Request;
        var response = context.HttpContext.Response;
        var eTagManager = context.HttpContext.RequestServices.GetService<IETagManager>()!;

        // For more info on this technique, see https://stackoverflow.com/a/65901913 and https://www.madskristensen.net/blog/send-etag-headers-in-aspnet-core/ and https://gist.github.com/madskristensen/36357b1df9ddbfd123162cd4201124c4
        var originalStream = response.Body;
        using MemoryStream memoryStream = new();

        response.Body = memoryStream;
        await next();
        memoryStream.Position = 0;

        if (response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created)
        {
            var responseHeaders = response.GetTypedHeaders();

            responseHeaders.CacheControl = new CacheControlHeaderValue() // how long clients should cache the response
            {
                Public = request.HasShowExtraHeader(),
                MaxAge = TimeSpan.FromSeconds(eTagManager.CacheTimeoutSeconds)
            };

            if (IsEtagSupported(response))
            {
                responseHeaders.ETag ??=
                    GenerateETag(memoryStream,
                        request.Path, eTagManager); // This request generates a hash from the response - this would come from S3 in live
            }
            
            var requestHeaders = request.GetTypedHeaders();

            if (IsClientCacheValid(requestHeaders, responseHeaders))
            {
                response.StatusCode = StatusCodes.Status304NotModified;

                // Remove all unnecessary headers while only keeping the ones that should be included in a `304` response.
                foreach (var header in response.Headers)
                    if (!HeadersToKeepFor304.Contains(header.Key))
                    {
                        response.Headers.Remove(header.Key);   
                    }

                return;
            }
        }

        await memoryStream
            .CopyToAsync(
                originalStream); // Writes anything the later middleware wrote to the body (and by extension our `memoryStream`) to the original response body stream, so that it will be sent back to the client as the response body.
    }
    
    private static bool IsEtagSupported(HttpResponse response)
    {
        // 20kb length limit - can be changed
        if (response.Body.Length > 20 * 1024) return false;

        if (response.Headers.ContainsKey(HeaderNames.ETag)) return false;

        return true;
    }

    private static EntityTagHeaderValue GenerateETag(Stream stream, string path, IETagManager eTagManager)
    {
        var hashBytes = MD5.HashData(stream);
        stream.Position = 0;
        var hashString = Convert.ToBase64String(hashBytes);

        var entityTagHeader = new EntityTagHeaderValue($"\"{hashString}\"");

        eTagManager.UpsertETag(path, entityTagHeader.Tag.ToString());
        return entityTagHeader;
    }

    private static bool IsClientCacheValid(RequestHeaders reqHeaders, ResponseHeaders resHeaders)
    {
        // If both `If-None-Match` and `If-Modified-Since` are present in a request, `If-None-Match` takes precedence and `If-Modified-Since` is ignored (provided, of course, that the resource supports entity-tags, hence the second condition after the `&&` operator in the following `if`). See https://datatracker.ietf.org/doc/html/rfc7232#section-3.3:~:text=A%20recipient%20MUST%20ignore%20If%2DModified%2DSince%20if
        if (reqHeaders.IfNoneMatch.Any() && resHeaders.ETag is not null)
            return reqHeaders.IfNoneMatch.Any(etag =>
                    etag.Compare(resHeaders.ETag,
                        false)
            );

        if (reqHeaders.IfModifiedSince is not null && resHeaders.LastModified is not null)
        {
            return reqHeaders.IfModifiedSince >= resHeaders.LastModified;
        }

        return false;
    }
}
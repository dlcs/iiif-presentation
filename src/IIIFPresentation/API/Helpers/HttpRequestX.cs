using System.Text;

namespace API.Helpers;

public static class HttpRequestX
{
    private const string SchemeDelimiter = "://";
    private const char PathDelimiter = '/';

    /// <summary>
    /// Generate a full display URL, deriving values from specified HttpRequest
    /// </summary>
    /// <param name="request">HttpRequest to generate display URL for</param>
    /// <param name="path">Path to append to URL</param>
    /// <param name="includeQueryParams">If true, query params are included in path. Else they are omitted</param>
    /// <returns>Full URL, including scheme, host, pathBase, path and queryString</returns>
    /// <remarks>
    /// based on Microsoft.AspNetCore.Http.Extensions.UriHelper.GetDisplayUrl(this HttpRequest request)
    /// </remarks>
    public static string GetDisplayUrl(this HttpRequest request, string? path = null, bool includeQueryParams = true)
    {
        var host = request.Host.HasValue ? request.Host.Value : string.Empty;
        var scheme = request.Scheme;
        var pathBase = request.PathBase.Value ?? string.Empty;
        var queryString = includeQueryParams
            ? request.QueryString.Value ?? string.Empty
            : string.Empty;
        if (!string.IsNullOrEmpty(path) && !path.StartsWith(PathDelimiter))
        {
            path = PathDelimiter + path;
        }
        var pathElement = path ?? string.Empty;

        // PERF: Calculate string length to allocate correct buffer size for StringBuilder.
        var length = scheme.Length + SchemeDelimiter.Length + host.Length
                     + pathBase.Length + pathElement.Length + queryString.Length;

        return new StringBuilder(length)
            .Append(scheme)
            .Append(SchemeDelimiter)
            .Append(host)
            .Append(pathBase)
            .Append(path)
            .Append(queryString)
            .ToString();
    }

    /// <summary>
    /// Generate a display URL, deriving values from specified HttpRequest. Omitting path and query string
    /// </summary>
    public static string GetBaseUrl(this HttpRequest request)
    {
        return request.GetDisplayUrl(null, false);
    }

    /// <summary>
    /// Get <see cref="HttpRequest"/> body as string
    /// </summary>
    public static async Task<string> GetRawRequestBodyAsync(this HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        using var streamReader = new StreamReader(request.Body);
        var rawRequestBody = await streamReader.ReadToEndAsync(cancellationToken);
        return rawRequestBody;
    }
}

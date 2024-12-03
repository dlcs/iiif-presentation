using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DLCS.Handlers;

internal class TimingHandler(ILogger<TimingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var path = request.RequestUri!.GetLeftPart(UriPartial.Path);
        logger.LogTrace("Calling {Uri}..", path);
        
        var result = await base.SendAsync(request, cancellationToken);
        
        sw.Stop();
        var elapsedMilliseconds = sw.ElapsedMilliseconds;
        var logLevel = GetLogLevel(elapsedMilliseconds);
        logger.Log(logLevel, "Request to {Uri} completed with status {StatusCode} in {Elapsed}ms", path,
            result.StatusCode, elapsedMilliseconds);
        return result;
    }

    private LogLevel GetLogLevel(long elapsedMilliseconds)
        => elapsedMilliseconds switch
        {
            >= 10000 => LogLevel.Warning,
            >= 3000 => LogLevel.Information,
            _ => LogLevel.Debug
        };
}
using Serilog.Context;
using Serilog.Core.Enrichers;

namespace BackgroundHandler.Helpers;

public static class LogContextHelpers
{
    /// <summary>
    /// Add "ServiceName" and optional "CorrelationId" properties to log context, which is then output as part of
    /// default log template.
    /// This is useful for filtering logs 
    /// </summary>
    public static IDisposable SetServiceName(string serviceName, string? messageId = null) =>
        LogContext.Push(
            new PropertyEnricher("ServiceName", serviceName),
            new PropertyEnricher("CorrelationId", messageId)
        );
}

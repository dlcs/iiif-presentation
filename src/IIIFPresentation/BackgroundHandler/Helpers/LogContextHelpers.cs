using Serilog.Context;

namespace BackgroundHandler.Helpers;

public static class LogContextHelpers
{
    /// <summary>
    /// Add "ServiceName" property to log context, which is then output as part of default log template.
    /// This is useful to filter logs as there can multiple processes running at the same time 
    /// </summary>
    public static IDisposable SetServiceName(string serviceName) =>
        LogContext.PushProperty("ServiceName", serviceName, false);
}
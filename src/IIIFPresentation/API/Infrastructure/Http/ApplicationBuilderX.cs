using API.Infrastructure.Http.CorrelationId;
using Microsoft.Extensions.Http;

namespace API.Infrastructure.Http;

public static class ApplicationBuilderX
{
    /// <summary>
    /// Propagate x-correlation-id header and user-agent to any downstream calls.
    /// NOTE: This will be added to ALL httpClient requests.
    /// </summary>
    public static IServiceCollection AddOutgoingHeaders(this IServiceCollection services)
    {
        services.AddSingleton<IHttpMessageHandlerBuilderFilter, OutgoingHeaderMessageHandlerBuilderFilter>();
        return services;
    }
}

internal class OutgoingHeaderMessageHandlerBuilderFilter(IHttpContextAccessor contextAccessor)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            builder.AdditionalHandlers.Add(new PropagateCorrelationIdHandler(contextAccessor));
            builder.AdditionalHandlers.Add(new SetUserAgentHandler());
            next(builder);
        };
    }
}
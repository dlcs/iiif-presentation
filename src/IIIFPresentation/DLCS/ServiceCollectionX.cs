using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using DLCS.API;
using DLCS.Handlers;
using Microsoft.Extensions.DependencyInjection;

[assembly:InternalsVisibleTo("DLCS.Tests")]

namespace DLCS;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add <see cref="IDlcsApiClient"/> to services collection.
    /// </summary>
    public static IServiceCollection AddDlcsApiClient(this IServiceCollection services,
        DlcsSettings dlcsSettings)
    {
        services
            .AddScoped<AmbientAuthHandler>()
            .AddTransient<TimingHandler>()
            .AddHttpClient<IDlcsApiClient, DlcsApiClient>(client =>
            {
                client.BaseAddress = dlcsSettings.ApiUri;
                client.Timeout = TimeSpan.FromMilliseconds(dlcsSettings.ApiDefaultTimeoutMs);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DlcsApiClient",
                    Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
            })
            .AddHttpMessageHandler<AmbientAuthHandler>()
            .AddHttpMessageHandler<TimingHandler>();
        
        return services;
    }
    
    /// <summary>
    /// Add <see cref="IDlcsOrchestratorClient"/> to services collection.
    /// </summary>
    public static IServiceCollection AddDlcsOrchestratorClient(this IServiceCollection services,
        DlcsSettings dlcsSettings)
    {
        services
            .AddTransient<TimingHandler>()
            .AddHttpClient<IDlcsOrchestratorClient, DlcsOrchestratorClient>(client => {
                client.Timeout = TimeSpan.FromMilliseconds(dlcsSettings.OrchestratorDefaultTimeoutMs);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DlcsOrchestratorClient",
                    Assembly.GetExecutingAssembly().GetName().Version?.ToString()));
            })
            .AddHttpMessageHandler<TimingHandler>();
        
        return services;
    }
}

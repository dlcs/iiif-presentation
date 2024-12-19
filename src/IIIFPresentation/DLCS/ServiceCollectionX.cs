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
    public static IServiceCollection AddDlcsClient(this IServiceCollection services,
        DlcsSettings dlcsSettings)
    {
        services
            .AddScoped<AmbientAuthHandler>()
            .AddTransient<TimingHandler>()
            .AddHttpClient<IDlcsApiClient, DlcsApiClient>(client =>
            {
                client.BaseAddress = dlcsSettings.ApiUri;
                client.Timeout = TimeSpan.FromMilliseconds(dlcsSettings.DefaultTimeoutMs);
            }).AddHttpMessageHandler<AmbientAuthHandler>()
            .AddHttpMessageHandler<TimingHandler>();
        
        return services;
    }
    
    /// <summary>
    /// Add <see cref="IDlcsApiClient"/> to services collection.
    /// </summary>
    public static IServiceCollection AddDlcsClientWithLocalAuth(this IServiceCollection services,
        DlcsSettings dlcsSettings)
    {
        services
            .AddScoped<AmbientAuthLocalHandler>()
            .AddTransient<TimingHandler>()
            .AddHttpClient<IDlcsApiClient, DlcsApiClient>(client =>
            {
                client.BaseAddress = dlcsSettings.ApiUri;
                client.Timeout = TimeSpan.FromMilliseconds(dlcsSettings.DefaultTimeoutMs);
            }).AddHttpMessageHandler<AmbientAuthLocalHandler>()
            .AddHttpMessageHandler<TimingHandler>();
        
        return services;
    }
}

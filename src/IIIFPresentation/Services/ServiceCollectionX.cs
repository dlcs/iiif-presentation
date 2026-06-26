using System.Net.Http.Headers;
using Core.Handlers;
using Core.Settings;
using Core.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Services.Manifests.Settings;
using Services.TextServices;

namespace Services;

public static class ServiceCollectionX
{
    public static void RegisterSharedServiceSettings(this WebApplicationBuilder builder)
    {
        var pathSettings = builder.Configuration.GetSection(PathSettings.SettingsName);
        builder.Services.Configure<PathSettings>(pathSettings);
        var typedPathTemplateOptions = pathSettings.GetSection(TypedPathTemplateOptions.SettingsName);
        builder.Services.Configure<TypedPathTemplateOptions>(typedPathTemplateOptions);
        builder.Services.Configure<ServicesSettings>(builder.Configuration.GetSection(ServicesSettings.SettingsName));
    }

    public static IServiceCollection AddTextBuilderClient(this IServiceCollection services,
        TextServicesSettings settings)
    {
        services.AddTransient<TimingHandler>();
        services.AddHttpClient<ITextBuilderClient, TextBuilderClient>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IIIF-Presentation", "1.0.0"));
            client.Timeout = TimeSpan.FromSeconds(settings.BuilderApiTimeoutSeconds);
        }).AddHttpMessageHandler<TimingHandler>();
        return services;
    }

    public static IServiceCollection AddTextSearchClient(this IServiceCollection services,
        TextServicesSettings settings)
    {
        services.AddTransient<TimingHandler>();
        services.AddHttpClient<ITextSearchClient, TextSearchClient>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IIIF-Presentation", "1.0.0"));
            client.Timeout = TimeSpan.FromSeconds(settings.SearchApiTimeoutSeconds);
        }).AddHttpMessageHandler<TimingHandler>();
        return services;
    }
}

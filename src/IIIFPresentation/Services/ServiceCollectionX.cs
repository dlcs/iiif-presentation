using Core.Web;
using DLCS;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Services.Manifests.Settings;

namespace Services;

public static class ServiceCollectionX
{
    public static void RegisterSharedServiceSettings(this WebApplicationBuilder builder)
    {
        var pathSettings = builder.Configuration.GetSection(PathSettings.SettingsName);
        builder.Services.Configure<PathSettings>(pathSettings);
        var typedPathTemplateOptions = pathSettings.GetSection(TypedPathTemplateOptions.SettingsName);
        builder.Services.Configure<TypedPathTemplateOptions>(typedPathTemplateOptions);
    }
}

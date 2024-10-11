using API.Settings;
using Microsoft.AspNetCore.Authentication;

namespace API.Auth;

internal static class ServiceCollectionX
{
    /// <summary>
    /// Add <see cref="DelegatedAuthHandler"/> to services collection.
    /// </summary>
    public static AuthenticationBuilder AddDelegatedAuthHandler(this IServiceCollection services,
        DlcsSettings dlcsSettings, Action<DelegatedAuthenticationOptions> configureOptions)
    {
        services.AddHttpClient<DelegatedAuthenticator>(client =>
        {
            client.BaseAddress = dlcsSettings.ApiUri;
        });
        
        return services
            .AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<DelegatedAuthenticationOptions, DelegatedAuthHandler>(
                BasicAuthenticationDefaults.AuthenticationScheme, configureOptions);
    }
}
using Microsoft.AspNetCore.Authentication;

namespace API.Auth;

internal static class ServiceCollectionX
{
    /// <summary>
    /// Add <see cref="DelegatedAuthHandler"/> to services collection.
    /// </summary>
    public static AuthenticationBuilder AddDelegatedAuthHandler(this IServiceCollection services,
        Action<DelegatedAuthenticationOptions> configureOptions)
    {
        return services
            .AddScoped<IAuthenticator, DelegatedAuthenticator>()
            .AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<DelegatedAuthenticationOptions, DelegatedAuthHandler>(
                BasicAuthenticationDefaults.AuthenticationScheme, configureOptions);
    }
}
using API.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration;

namespace API.Tests.Integration.Infrastructure;

public static class PresentationAppFactoryX
{
    /// <summary>
    /// Configure app factory to use connection string from DBFixture and configure test authenticator logic.
    /// Takes an additional delegate to do additional setup
    /// </summary>
    public static HttpClient ConfigureBasicIntegrationTestHttpClient<T>(
        this PresentationAppFactory<T> factory,
        PresentationContextFixture dbFixture,
        Func<PresentationAppFactory<T>, PresentationAppFactory<T>>? additionalSetup = null,
        Action<IServiceCollection>? additionalTestServices = null) where T : class
    {
        additionalSetup ??= f => f;
        
        var configuredFactory = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddSingleton<IAuthenticator, TestAuthenticator>();
                additionalTestServices?.Invoke(services);
            });

        var httpClient = additionalSetup(configuredFactory)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        return httpClient;
    }
}

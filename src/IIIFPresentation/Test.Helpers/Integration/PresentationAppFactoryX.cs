using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Test.Helpers.Integration;

public static class PresentationAppFactoryX
{
    /// <summary>
    /// Configure app factory to use connection string from DBFixture.
    /// Takes an additional delegate to do additional setup
    /// </summary>
    public static HttpClient ConfigureBasicIntegrationTestHttpClient<T>(
        this PresentationAppFactory<T> factory,
        PresentationContextFixture dbFixture,
        Func<PresentationAppFactory<T>, PresentationAppFactory<T>>? additionalSetup = null) where T : class
    {
        additionalSetup ??= f => f;
        
        var configuredFactory = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                string authenticationScheme = "Api-Test";
                services.AddAuthentication(authenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        authenticationScheme, _ => { });
            });

        var httpClient = additionalSetup(configuredFactory)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        return httpClient;
    }
}
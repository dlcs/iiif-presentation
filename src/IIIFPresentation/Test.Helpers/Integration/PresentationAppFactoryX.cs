using Microsoft.AspNetCore.Mvc.Testing;

namespace Test.Helpers.Integration;

public static class PresentationAppFactoryX
{
    /// <summary>
    /// Configure app factory to use connection string from DBFixture
    /// </summary>
    public static HttpClient ConfigureBasicIntegrationTestHttpClient<T>(
        this PresentationAppFactory<T> factory,
        PresentationContextFixture dbFixture,
        string authenticationScheme) where T : class
        => ConfigureBasicIntegrationTestHttpClient(factory, dbFixture, authenticationScheme, null);
    
    /// <summary>
    /// Configure app factory to use connection string from DBFixture.
    /// Takes an additional delegate to do additional setup
    /// </summary>
    public static HttpClient ConfigureBasicIntegrationTestHttpClient<T>(
        this PresentationAppFactory<T> factory,
        PresentationContextFixture dbFixture,
        string authenticationScheme,
        Func<PresentationAppFactory<T>, PresentationAppFactory<T>> additionalSetup) where T : class
    {
        additionalSetup ??= f => f;
        
        var configuredFactory = factory
            .WithConnectionString(dbFixture.ConnectionString);

        var httpClient = additionalSetup(configuredFactory)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        return httpClient;
    }
}
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#nullable disable

namespace Test.Helpers.Integration;

/// <summary>
/// Basic appFactory for the presentation API
/// </summary>
/// <typeparam name="TProgram"></typeparam>
public class PresentationAppFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly Dictionary<string, string> configuration = new();
    private readonly List<IDisposable> disposables = new();
    private Action<IServiceCollection> configureTestServices;
    
    /// <summary>
    /// Specify connection string to use for presentationContext when building services
    /// </summary>
    /// <param name="connectionString">connection string to use for dbContext - docker instance</param>
    /// <returns>Current instance</returns>
    public PresentationAppFactory<TProgram> WithConnectionString(string connectionString)
    {
        configuration["ConnectionStrings:PostgreSQLConnection"] = connectionString;
        return this;
    }
    
    /// <summary>
    /// Specify a configuration value to be set in appFactory
    /// </summary>
    /// <param name="key">Key of setting to update, in format ("Thumbs:ThumbsBucket")</param>
    /// <param name="value">Value to set</param>
    /// <returns>Current instance</returns>
    public PresentationAppFactory<TProgram> WithConfigValue(string key, string value)
    {
        configuration[key] = value;
        return this;
    }

    /// <summary>
    /// <see cref="IDisposable"/> implementation that will be disposed of alongside appfactory
    /// </summary>
    public PresentationAppFactory<TProgram> WithDisposable(IDisposable disposable)
    {
        disposables.Add(disposable);
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var projectDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(projectDir, "appsettings.Testing.json");

        builder
            .ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddJsonFile(configPath);
                conf.AddInMemoryCollection(configuration);
            })
            .ConfigureTestServices(services =>
            {
                if (configureTestServices != null)
                {
                    configureTestServices(services);
                }
            })
            .UseEnvironment("Testing")
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = true;
            });
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var d in disposables)
        {
            d.Dispose();
        }
        base.Dispose(disposing);
    }
}
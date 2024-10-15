using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    private LocalStackFixture localStack;
    
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
    /// Action to call in ConfigureTestServices
    /// </summary>
    /// <returns>Current instance</returns>
    public PresentationAppFactory<TProgram> WithTestServices(Action<IServiceCollection> configureTestServices)
    {
        this.configureTestServices = configureTestServices;
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
    
    /// <summary>
    /// <see cref="LocalStackFixture"/> to use for replacing AWS services.
    /// </summary>
    /// <param name="fixture"><see cref="LocalStackFixture"/> to use.</param>
    /// <returns>Current instance</returns>
    public PresentationAppFactory<TProgram> WithLocalStack(LocalStackFixture fixture)
    {
        localStack = fixture;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .ConfigureTestServices(services =>
            {
                if (configureTestServices != null)
                {
                    configureTestServices(services);
                }
                
                if (localStack != null)
                {
                    ConfigureAWSServices(services);
                }
            })
            .UseEnvironment("Testing")
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = true;
            });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var projectDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(projectDir, "appsettings.Testing.json");
        
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(configuration);
            config.AddJsonFile(configPath);
        });
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var d in disposables)
        {
            d.Dispose();
        }
        base.Dispose(disposing);
    }
    
    private void ConfigureAWSServices(IServiceCollection services)
    {
        services.Remove(new ServiceDescriptor(typeof(IAmazonS3),
            a => a.GetService(typeof(IAmazonS3)), ServiceLifetime.Singleton));
        services.AddSingleton<IAmazonS3>(p => localStack.AWSS3ClientFactory());
    }
}
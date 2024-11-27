using API.Features.CustomerCreation;
using API.Settings;
using AWS.Configuration;
using AWS.S3;
using AWS.Settings;
using AWS.SQS;
using Microsoft.DotNet.Scaffolding.Shared;
using Repository;

namespace BackgroundHandler.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Configure AWS services. Generic, non project-specific
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        var awsBuilder = services
            .SetupAWS(configuration, hostEnvironment)
            .WithAmazonSQS();

        services
            .AddSingleton<SqsListener>()
            .AddSingleton<SqsQueueUtilities>();
        

        return services;
    }

    /// <summary>
    /// Configure BackgroundWorker + handler services
    /// </summary>
    public static IServiceCollection AddQueueMonitoring(this IServiceCollection services, AWSSettings aws)
    {
        if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
        {
            services
                .AddHostedService<CustomerCreatedListenerService>()
                .AddScoped<CustomerCreatedMessageHandler>();
        }

        return services;
    }

    /// <summary>
    /// Add required caching dependencies
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, CacheSettings cacheSettings)
        => services
            .AddMemoryCache(memoryCacheOptions =>
            {
                memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
            })
            .AddLazyCache();
}

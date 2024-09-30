using System.Reflection;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Mediatr.Behaviours;
using API.Infrastructure.Requests.Pipelines;
using API.Settings;
using AWS.Configuration;
using AWS.S3;
using MediatR;
using Repository;
using Sqids;

namespace API.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add all dataaccess dependencies, including repositories and presentation context
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddPresentationContext(configuration);
    }
    
    /// <summary>
    /// Configure caching
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, CacheSettings cacheSettings)
        => services.AddMemoryCache(memoryCacheOptions =>
            {
                memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
            })
            .AddLazyCache();

    /// <summary>
    /// Add MediatR services and pipeline behaviours to service collection.
    /// </summary>
    public static IServiceCollection ConfigureMediatR(this IServiceCollection services)
    {
        return services
            .AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehaviour<,>));
    }

    public static IServiceCollection ConfigureIdGenerator(this IServiceCollection services)
    {
        return services.AddSingleton(new SqidsEncoder<long>(new()
            {
                Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789",
                MinLength = 6,
            }))
            .AddSingleton<IIdGenerator, SqidsGenerator>();
    }
    
    /// <summary>
    /// Add required AWS services
    /// </summary>
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services
            .AddSingleton<IBucketReader, S3BucketReader>()
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3();

        return services;
    }
}
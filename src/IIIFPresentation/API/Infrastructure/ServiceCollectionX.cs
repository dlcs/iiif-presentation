using System.Reflection;
using API.Features.CustomerCreation;
using API.Infrastructure.AWS;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Mediatr.Behaviours;
using API.Infrastructure.Requests.Pipelines;
using API.Settings;
using AWS.Configuration;
using AWS.S3;
using AWS.Settings;
using AWS.SQS;
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
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment, AWSSettings aws)
    {
        services
            .AddSingleton<IBucketReader, S3BucketReader>()
            .AddSingleton<IBucketWriter, S3BucketWriter>()
            .AddSingleton<IIIFS3Service>();

        var awsBuilder = services
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3();

        if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
        {
            services
                .AddSingleton<SqsListener>()
                .AddSingleton<SqsQueueUtilities>()
                .AddHostedService<CustomerCreatedListenerService>()
                .AddScoped<CustomerCreatedMessageHandler>();

            awsBuilder.WithAmazonSQS();
        }

        return services;
    }
    
    /// <summary>
    /// Add Cors policy allowing any Origin, Method and Header
    /// </summary>
    /// <param name="services">Current <see cref="IServiceCollection"/> object</param>
    /// <param name="policyName">Cors policy name</param>
    public static IServiceCollection ConfigureDefaultCors(this IServiceCollection services, string policyName)
        => services.AddCors(options =>
        {
            options.AddPolicy(policyName, builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });
}
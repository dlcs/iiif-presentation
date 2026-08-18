using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests.Pipelines;
using API.Settings;
using AWS.Configuration;
using AWS.Helpers;
using AWS.S3;
using Mediator;
using Microsoft.OpenApi;
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
    /// Add Mediator services and pipeline behaviours to service collection.
    /// </summary>
    public static IServiceCollection ConfigureMediator(this IServiceCollection services)
    {
        return services
            .AddMediator(options =>
            {
                // Handlers depend on scoped services (PresentationContext etc); Mediator defaults to
                // Singleton, which would capture those as long-lived dependencies.
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(CacheInvalidationBehaviour<,>)];
            });
    }

    /// <summary>
    /// Add services for identity generation
    /// </summary>
    public static IServiceCollection ConfigureIdGenerator(this IServiceCollection services)
    {
        return services.AddSingleton(new SqidsEncoder<long>(new()
            {
                Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789",
                MinLength = 6,
            }))
            .AddSingleton<IIdGenerator, SqidsGenerator>()
            .AddScoped<IdentityManager>();
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
            .AddSingleton<IIIIFS3Service, IIIFS3Service>();

        services
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonS3();

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
    
    /// <summary>
    /// Add SwaggerGen services to service collection.
    /// </summary>
    public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
        => services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IIIF Presentation API", 
                Version = "v1",
                Description = "API for creation and management of IIIF Presentation API resources"
            });

            c.AddSecurityDefinition(
                "basic", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "basic",
                    In = ParameterLocation.Header,
                    Description = "Basic Authorization header",
                });

            c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("basic", document)] = []
            });
        });
}

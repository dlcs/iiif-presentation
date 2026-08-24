using System.Reflection;
using API.Infrastructure.IdGenerator;
using API.Infrastructure.Requests.Pipelines;
using API.Settings;
using AWS.Configuration;
using AWS.Helpers;
using AWS.S3;
using MediatR;
using Microsoft.OpenApi;
using Repository;
using Sqids;

namespace API.Infrastructure;

public static class ServiceCollectionX
{
    /// <param name="services">Current <see cref="IServiceCollection"/> object</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Add all dataaccess dependencies, including repositories and presentation context
        /// </summary>
        public IServiceCollection AddDataAccess(IConfiguration configuration)
        {
            return services
                .AddPresentationContext(configuration);
        }

        /// <summary>
        /// Configure caching
        /// </summary>
        public IServiceCollection AddCaching(CacheSettings cacheSettings)
            => services.AddMemoryCache(memoryCacheOptions =>
                {
                    memoryCacheOptions.SizeLimit = cacheSettings.MemoryCacheSizeLimit;
                    memoryCacheOptions.CompactionPercentage = cacheSettings.MemoryCacheCompactionPercentage;
                })
                .AddLazyCache();

        /// <summary>
        /// Add MediatR services and pipeline behaviours to service collection.
        /// </summary>
        public IServiceCollection ConfigureMediatR()
        {
            return services
                .AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
                .AddScoped(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehaviour<,>));
        }

        /// <summary>
        /// Add services for identity generation
        /// </summary>
        public IServiceCollection ConfigureIdGenerator()
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
        public IServiceCollection AddAws(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
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
        /// <param name="policyName">Cors policy name</param>
        public IServiceCollection ConfigureDefaultCors(string policyName)
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
        public IServiceCollection ConfigureSwagger()
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
}

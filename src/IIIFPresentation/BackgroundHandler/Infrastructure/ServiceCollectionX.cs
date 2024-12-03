﻿using AWS.Configuration;
using AWS.Settings;
using AWS.SQS;
using BackgroundHandler.CustomerCreation;
using BackgroundHandler.Listener;
using Repository;

namespace BackgroundHandler.Infrastructure;

public static class ServiceCollectionX
{
    public static IServiceCollection AddAws(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        services
            .AddSingleton<SqsListener>()
            .AddSingleton<SqsQueueUtilities>();
        
        services
            .SetupAWS(configuration, webHostEnvironment)
            .WithAmazonSQS();
        
        return services;
    }
    
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, AWSSettings aws)
    {
        
        if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
        {
            services
                .AddHostedService(sp => 
                    ActivatorUtilities.CreateInstance<CreateBackgroundListenerService<CustomerCreatedMessageHandler>>(sp, aws.SQS.CustomerCreatedQueueName))
                .AddScoped<CustomerCreatedMessageHandler>();
        }

        return services;
    }
    
    /// <summary>
    /// Add all dataaccess dependencies, including repositories and presentation context
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddPresentationContext(configuration);
    }
}
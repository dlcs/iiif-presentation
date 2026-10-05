using AWS.Configuration;
using AWS.Helpers;
using AWS.S3;
using AWS.Settings;
using AWS.SQS;
using BackgroundHandler.BatchCompletion;
using BackgroundHandler.CustomerCreation;
using BackgroundHandler.Listener;
using BackgroundHandler.TextCompletion;
using Repository;

namespace BackgroundHandler.Infrastructure;

public static class ServiceCollectionX
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAws(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            services
                .AddSingleton<IBucketReader, S3BucketReader>()
                .AddSingleton<IBucketWriter, S3BucketWriter>()
                .AddSingleton<SqsListener>()
                .AddSingleton<SqsQueueUtilities>()
                .AddSingleton<IIIIFS3Service, IIIFS3Service>();
        
            services
                .SetupAWS(configuration, webHostEnvironment)
                .WithAmazonSQS()
                .WithAmazonS3();
        
            return services;
        }

        public IServiceCollection AddBackgroundServices(AWSSettings aws)
        {
            if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
            {
                services
                    .AddHostedService(sp => 
                        ActivatorUtilities.CreateInstance<CreateBackgroundListenerService<CustomerCreatedMessageHandler>>(sp, aws.SQS.CustomerCreatedQueueName))
                    .AddScoped<CustomerCreatedMessageHandler>();
            }
        
            if (!string.IsNullOrEmpty(aws.SQS.BatchCompletionQueueName))
            {
                services
                    .AddHostedService(sp =>
                        ActivatorUtilities.CreateInstance<CreateBackgroundListenerService<BatchCompletionMessageHandler>>(sp, aws.SQS.BatchCompletionQueueName))
                    .AddScoped<BatchCompletionMessageHandler>();
            }

            if (!string.IsNullOrEmpty(aws.SQS.TextJobQueueName))
            {
                services
                    .AddHostedService(sp =>
                        ActivatorUtilities.CreateInstance<CreateBackgroundListenerService<TextServiceJobCompletionMessageHandler>>(sp, aws.SQS.TextJobQueueName))
                    .AddScoped<TextServiceJobCompletionMessageHandler>();
            }

            return services;
        }

        /// <summary>
        /// Add all dataaccess dependencies, including repositories and presentation context
        /// </summary>
        public IServiceCollection AddDataAccess(IConfiguration configuration)
        {
            return services
                .AddPresentationContext(configuration);
        }
    }
}

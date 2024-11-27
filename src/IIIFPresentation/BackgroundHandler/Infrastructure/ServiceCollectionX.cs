using AWS.Configuration;
using AWS.Settings;
using AWS.SQS;
using BackgroundHandler.CustomerCreation;
using BackgroundHandler.Listener;

namespace BackgroundHandler.Infrastructure;

public static class ServiceCollectionX
{
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, 
        IConfiguration configuration, IWebHostEnvironment webHostEnvironment, AWSSettings aws)
    {
        services
            .AddSingleton<SqsListener>()
            .AddSingleton<SqsQueueUtilities>();

        if (services.FirstOrDefault(x => x.ServiceType == typeof(AwsBuilder))?
                .ImplementationInstance is AwsBuilder awsBuilder)
        {
            awsBuilder
                .WithAmazonSQS();
        }
        else
        {
            services
                .SetupAWS(configuration, webHostEnvironment)
                .WithAmazonS3()
                .WithAmazonSQS();
        }
        
        if (!string.IsNullOrEmpty(aws.SQS.CustomerCreatedQueueName))
        {
            services
                .AddHostedService<CreateBackgroundListenerService<CustomerCreatedMessageHandler>>()
                .AddScoped<CustomerCreatedMessageHandler>();
        }

        return services;
    }
}
using System.Reflection;
using API.Infrastructure.Mediatr.Behaviours;
using API.Infrastructure.Requests.Pipelines;
using MediatR;
using Repository;

namespace API.Infrastructure;

public static class ServiceCollectionX
{
    /// <summary>
    ///     Add all dataaccess dependencies, including repositories and presentation context
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .AddPresentationContext(configuration);
    }

    /// <summary>
    ///     Add MediatR services and pipeline behaviours to service collection.
    /// </summary>
    public static IServiceCollection ConfigureMediatR(this IServiceCollection services)
    {
        return services
            .AddMediatR(config => config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(CacheInvalidationBehaviour<,>));
    }
}
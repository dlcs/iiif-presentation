namespace API.Infrastructure.Http;

public static class EndpointRouteBuilderX
{
    public static RouteHandlerBuilder AddVersionEndpoint(this IEndpointRouteBuilder endpoints, string path = "/version")
    {
        return endpoints.MapGet(path,
            () => Results.Ok(new { version = Environment.GetEnvironmentVariable("APP_VERSION") ?? "unknown" }));
    }
}

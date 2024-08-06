
using API.Settings;
using API.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Repository;
using Serilog;

namespace API.Tests;

public class Startup
{
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment webHostEnvironment;

    public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
    {
        this.configuration = configuration;
        this.webHostEnvironment = webHostEnvironment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ApiSettings>().Bind(configuration)
            .ValidateFluentValidation()
            .ValidateOnStart();

        services
            .Configure<ApiSettings>(configuration.GetSection("ApiSettings"));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        app.UseForwardedHeaders();
        if (env.IsDevelopment())
        {
            IiifPresentationContextConfiguration.TryRunMigrations(configuration, logger);
            app.UseDeveloperExceptionPage();
        }
        
        app
            .UseRouting()
            .UseSerilogRequestLogging()
            .UseEndpoints(endpoints =>
            {
                endpoints
                    .MapControllers()
                    .RequireAuthorization();
                endpoints.MapHealthChecks("/ping").AllowAnonymous();
            });
    }
}
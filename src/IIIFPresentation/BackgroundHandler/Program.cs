

using Serilog;

public class Program
{
     public static async Task Main(string[] args)
    {
    //     Log.Logger = new LoggerConfiguration()
    //         .Enrich.FromLogContext()
    //         .WriteTo.Console()
    //         .CreateLogger();
    //
    //     try
    //     {
    //         await CreateHostBuilder(args).Build().RunAsync();
    //     }
    //     catch (Exception ex)
    //     {
    //         Log.Fatal(ex, "Application start-up failed");
    //     }
    //     finally
    //     {
    //         Log.CloseAndFlush();
    //     }
     }

    // public static IHostBuilder CreateHostBuilder(string[] args) =>
    //     Host.CreateDefaultBuilder(args)
    //         .ConfigureServices((hostContext, services) =>
    //         {
    //             services
    //                 .Configure<CleanupHandlerSettings>(hostContext.Configuration)
    //                 .AddAws(hostContext.Configuration, hostContext.HostingEnvironment)
    //                 .AddDataAccess(hostContext.Configuration, hostContext.Configuration.Get<CleanupHandlerSettings>())
    //                 .AddCaching(hostContext.Configuration.GetSection("Caching").Get<CacheSettings>())
    //                 .AddQueueMonitoring();
    //         })
    //         .UseSerilog((hostingContext, loggerConfiguration)
    //             => loggerConfiguration
    //                 .ReadFrom.Configuration(hostingContext.Configuration)
    //         )
    //         .ConfigureAppConfiguration((context, builder) =>
    //         {
    //             builder.AddSystemsManager(context);
    //         });
}
using Microsoft.EntityFrameworkCore;

namespace Repository;

public static class IiifPresentationContextConfiguration
{
    private static readonly string ConnectionStringKey = "PostgreSQLConnection";
    private static readonly string RunMigrationsKey = "RunMigrations";

    /// <summary>
    ///     Register and configure <see cref="PresentationContext" />
    /// </summary>
    public static IServiceCollection AddPresentationContext(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddDbContext<PresentationContext>(options => SetupOptions(configuration, options));
    }

    /// <summary>
    ///     Run EF migrations if "RunMigrations" = true
    /// </summary>
    public static void TryRunMigrations(IConfiguration configuration, ILogger logger)
    {
        if (configuration.GetValue(RunMigrationsKey, false))
        {
            using var context = new PresentationContext(GetOptionsBuilder(configuration).Options);

            var pendingMigrations = context.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Count == 0)
            {
                logger.LogInformation("No migrations to run");
                return;
            }

            logger.LogInformation("Running migrations: {Migrations}", string.Join(",", pendingMigrations));
            context.Database.Migrate();
        }
    }

    /// <summary>
    ///     Get a new instantiated <see cref="PresentationContext" /> object
    /// </summary>
    public static PresentationContext GetNewDbContext(IConfiguration configuration)
    {
        return new PresentationContext(GetOptionsBuilder(configuration).Options);
    }

    private static DbContextOptionsBuilder<PresentationContext> GetOptionsBuilder(IConfiguration configuration)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PresentationContext>();
        SetupOptions(configuration, optionsBuilder);
        return optionsBuilder;
    }

    private static void SetupOptions(IConfiguration configuration,
        DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString(ConnectionStringKey))
            .UseSnakeCaseNamingConvention();
    }
}
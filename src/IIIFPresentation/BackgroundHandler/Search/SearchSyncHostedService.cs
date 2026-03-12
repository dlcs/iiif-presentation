using Microsoft.Extensions.Configuration;
using Services.Search;

namespace BackgroundHandler.Search;

public class SearchSyncHostedService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SearchSyncHostedService> logger,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCycle(stoppingToken);

        var settings = configuration.GetSection(TypesenseSettings.SettingsName).Get<TypesenseSettings>()
                       ?? new TypesenseSettings();
        var interval = TimeSpan.FromMinutes(Math.Max(settings.BatchWindowMinutes, 1));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycle(stoppingToken);
        }
    }

    private async Task RunCycle(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var searchSyncService = scope.ServiceProvider.GetRequiredService<ISearchSyncService>();

        try
        {
            await searchSyncService.RunOnce(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search sync cycle failed");
        }
    }
}

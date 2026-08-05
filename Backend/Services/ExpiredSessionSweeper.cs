namespace Backend.Services;

public class ExpiredSessionSweeper(IServiceScopeFactory scopeFactory, ILogger<ExpiredSessionSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
            var removed = await sessionService.DeleteExpiredSessionsAsync(cancellationToken);

            if (removed > 0)
            {
                logger.LogInformation("Removed {Count} expired session(s).", removed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Expired session sweep failed.");
        }
    }
}

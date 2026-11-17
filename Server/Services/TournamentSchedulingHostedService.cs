namespace ShadowverseBeyond.Server.Services;

public sealed class TournamentSchedulingHostedService(
    IServiceScopeFactory scopes, ILogger<TournamentSchedulingHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var discovery = scopes.CreateAsyncScope();
                var due = await discovery.ServiceProvider.GetRequiredService<TournamentScheduling>()
                    .DueEventsAsync(stoppingToken);
                foreach (var id in due)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<TournamentScheduling>().AdvanceAsync(id, stoppingToken);
                    }
                    catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(error, "Tournament schedule could not advance for {TournamentId}.", id);
                    }
                }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Could not read tournaments waiting to advance.");
            }
        }
    }
}

namespace ShadowverseBeyond.Server.Services;

public static class RoomLifetime
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ClosedRetention = TimeSpan.FromMinutes(2);
}

public sealed class RoomCleanupHostedService(
    Abstractions.IMatchService matches, TakeTwoService drafts,
    ILogger<RoomCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { matches.CleanupExpired(); drafts.CleanupExpired(); }
                catch (Exception error) { logger.LogError(error, "Room cleanup failed"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

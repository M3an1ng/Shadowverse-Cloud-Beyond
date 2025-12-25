using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShadowverseBeyond.Server.Infrastructure.Data;
using ShadowverseBeyond.Server.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Services
{
    public class ReplayCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReplayCleanupHostedService> _logger;
        private readonly ReplayOptions _options;

        public ReplayCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReplayCleanupHostedService> logger,
            IOptions<ReplayOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.RetentionDays <= 0)
            {
                _logger.LogInformation("Replay cleanup disabled (RetentionDays <= 0).");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CleanupIntervalMinutes));
            _logger.LogInformation("Replay cleanup started. RetentionDays={RetentionDays} Interval={Interval}", _options.RetentionDays, interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOnceAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Replay cleanup failed (will retry).");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CleanupOnceAsync(CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expiredGameIds = await db.ReplayGames
                .AsNoTracking()
                .Where(g => (g.EndedAtUtc != null && g.EndedAtUtc < cutoff) || (g.EndedAtUtc == null && g.CreatedAtUtc < cutoff))
                .Select(g => g.GameId)
                .ToListAsync(cancellationToken);

            if (expiredGameIds.Count == 0) return;

            foreach (var gameId in expiredGameIds)
            {
                var deletedEvents = await db.ReplayEvents
                    .Where(e => e.GameId == gameId)
                    .ExecuteDeleteAsync(cancellationToken);

                var deletedBundles = await db.ReplayBundles
                    .Where(b => b.GameId == gameId)
                    .ExecuteDeleteAsync(cancellationToken);

                var deletedSnapshots = await db.ReplaySnapshots
                    .Where(s => s.GameId == gameId)
                    .ExecuteDeleteAsync(cancellationToken);

                var deletedGames = await db.ReplayGames
                    .Where(g => g.GameId == gameId)
                    .ExecuteDeleteAsync(cancellationToken);

                if (deletedGames > 0)
                {
                    ReplayMetrics.CleanupDeletedGames.Add(deletedGames);
                }

                _logger.LogInformation(
                    "Replay cleanup deleted GameId={GameId}: Games={Games} Bundles={Bundles} Events={Events} Snapshots={Snapshots}",
                    gameId, deletedGames, deletedBundles, deletedEvents, deletedSnapshots);
            }
        }
    }
}

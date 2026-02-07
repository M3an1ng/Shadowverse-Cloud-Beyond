namespace ShadowverseBeyond.Server.Services.Abstractions;

public interface IGuestDeckMigrationService
{
    Task<GuestDeckMigrationResult> MigrateAsync(Guid guestId, Guid userId, CancellationToken cancellationToken);
}

public record GuestDeckMigrationResult(int MigratedCount, int FailedCount);

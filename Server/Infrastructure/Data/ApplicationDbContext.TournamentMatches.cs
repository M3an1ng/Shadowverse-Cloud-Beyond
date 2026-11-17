using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Infrastructure.Data;

public partial class ApplicationDbContext
{
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();
    public DbSet<TournamentMatchGame> TournamentMatchGames => Set<TournamentMatchGame>();

    private static void ConfigureTournamentMatches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentMatch>(entity =>
        {
            entity.ToTable("TournamentMatches");
            entity.Property(match => match.Id).ValueGeneratedNever();
            entity.HasIndex(match => new { match.TournamentId, match.StageNumber, match.Bracket, match.RoundNumber, match.PairingNumber }).IsUnique();
            entity.HasOne<TournamentStage>().WithMany().HasForeignKey(match => new { match.TournamentId, Number = match.StageNumber });
            entity.HasOne<TournamentEntry>().WithMany().HasForeignKey(match => match.EntryOneId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TournamentEntry>().WithMany().HasForeignKey(match => match.EntryTwoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(match => match.Games).WithOne().HasForeignKey(game => game.MatchId);
        });

        modelBuilder.Entity<TournamentMatchGame>(entity =>
        {
            entity.ToTable("TournamentMatchGames");
            entity.Property(game => game.Id).ValueGeneratedNever();
            entity.HasIndex(game => new { game.MatchId, game.Number }).IsUnique();
            entity.HasOne<TournamentEntryDeck>().WithMany().HasForeignKey(game => game.EntryOneDeckId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TournamentEntryDeck>().WithMany().HasForeignKey(game => game.EntryTwoDeckId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

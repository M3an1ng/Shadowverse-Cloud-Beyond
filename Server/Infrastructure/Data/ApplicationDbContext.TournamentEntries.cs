using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities;

namespace ShadowverseBeyond.Server.Infrastructure.Data;

public partial class ApplicationDbContext
{
    public DbSet<TournamentEntry> TournamentEntries => Set<TournamentEntry>();
    public DbSet<TournamentEntryDeck> TournamentEntryDecks => Set<TournamentEntryDeck>();
    public DbSet<TournamentStageEntry> TournamentStageEntries => Set<TournamentStageEntry>();

    private static void ConfigureTournamentEntries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TournamentEntry>(entity =>
        {
            entity.ToTable("TournamentEntries");
            entity.Property(entry => entry.Id).ValueGeneratedNever();
            entity.Property(entry => entry.PlayerName).HasMaxLength(50);
            entity.Property(entry => entry.Revision).IsConcurrencyToken();
            entity.HasIndex(entry => new { entry.TournamentId, entry.UserId }).IsUnique();
            entity.HasOne<Tournament>().WithMany().HasForeignKey(entry => entry.TournamentId);
            entity.HasOne<User>().WithMany().HasForeignKey(entry => entry.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(entry => entry.Decks).WithOne().HasForeignKey(deck => deck.EntryId);
        });

        modelBuilder.Entity<TournamentEntryDeck>(entity =>
        {
            entity.ToTable("TournamentEntryDecks");
            entity.Property(deck => deck.Id).ValueGeneratedNever();
            entity.Property(deck => deck.Name).HasMaxLength(100);
            entity.Property(deck => deck.Archetype).HasMaxLength(64);
            entity.HasIndex(deck => new { deck.EntryId, deck.StageNumber, deck.Slot }).IsUnique();
        });

        modelBuilder.Entity<TournamentStageEntry>(entity =>
        {
            entity.ToTable("TournamentStageEntries");
            entity.HasKey(entry => new { entry.TournamentId, entry.StageNumber, entry.EntryId });
            entity.HasIndex(entry => new { entry.TournamentId, entry.StageNumber, entry.Seed }).IsUnique();
            entity.HasOne<TournamentStage>().WithMany()
                .HasForeignKey(entry => new { entry.TournamentId, Number = entry.StageNumber });
            entity.HasOne<TournamentEntry>().WithMany().HasForeignKey(entry => entry.EntryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

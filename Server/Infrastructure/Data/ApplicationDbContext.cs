using Microsoft.EntityFrameworkCore;
using ShadowverseBeyond.Server.Domain.Entities; // Added ShadowverseBeyond prefix

namespace ShadowverseBeyond.Server.Infrastructure.Data; // Added ShadowverseBeyond prefix

/// <summary>
/// Represents the database context for the application, interfacing with EF Core.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// Required for dependency injection.
    /// </summary>
    /// <param name="options">The options to be used by a DbContext.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the DbSet for CardDefinition entities. Represents the CardDefinitions table.
    /// </summary>
    public DbSet<CardDefinition> CardDefinitions { get; set; } = null!; // The 'null!' avoids nullable warnings, EF Core initializes it.

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types
    /// exposed in <see cref="DbSet{TEntity}"/> properties on your derived context.
    /// The resulting model may be cached and re-used for subsequent instances of your derived context.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicitly configure the table name for CardDefinition.
        // While EF Core might infer "CardDefinitions" correctly from the DbSet property name,
        // being explicit can prevent potential issues or naming conflicts.
        modelBuilder.Entity<CardDefinition>().ToTable("CardDefinitions");

        // Potential future configurations:
        // - If storing Keywords as a delimited string:
        // modelBuilder.Entity<CardDefinition>()
        //     .Property(e => e.Keywords)
        //     .HasConversion(
        //         v => v == null ? null : string.Join(',', v), // Handle null list
        //         v => string.IsNullOrEmpty(v) ? new List<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() // Handle null/empty string
        //      );

        // - Configure EffectsJson column type (SQLite uses TEXT by default, usually sufficient)
        // modelBuilder.Entity<CardDefinition>()
        //     .Property(e => e.EffectsJson)
        //     .HasColumnType("TEXT");

        // Add configuration for other entities or complex property mappings here later.
    }
}
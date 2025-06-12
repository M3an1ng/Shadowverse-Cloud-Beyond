//
// 相关文档:
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
// - 实体关系图: 文档/04-数据库设计/01-实体关系图.md
// - 配置管理系统: 文档/05-游戏系统/09-配置管理系统.md
//
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
  /// Gets or sets the DbSet for CardAbility entities. Represents the CardAbilities table.
  /// </summary>
  public DbSet<AbilityDefinition> CardAbilities { get; set; } = null!;

  // 用户管理相关实体
  /// <summary>
  /// Gets or sets the DbSet for User entities. Represents the Users table.
  /// </summary>
  public DbSet<User> Users { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserProfile entities. Represents the UserProfiles table.
  /// </summary>
  public DbSet<UserProfile> UserProfiles { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for GameSession entities. Represents the GameSessions table.
  /// </summary>
  public DbSet<GameSession> GameSessions { get; set; } = null!;

  // 经济系统相关实体
  /// <summary>
  /// Gets or sets the DbSet for UserEconomy entities. Represents the UserEconomies table.
  /// </summary>
  public DbSet<UserEconomy> UserEconomies { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for CoinTransaction entities. Represents the CoinTransactions table.
  /// </summary>
  public DbSet<CoinTransaction> CoinTransactions { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for CardPackDefinition entities. Represents the CardPackDefinitions table.
  /// </summary>
  public DbSet<CardPackDefinition> CardPackDefinitions { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserCardPack entities. Represents the UserCardPacks table.
  /// </summary>
  public DbSet<UserCardPack> UserCardPacks { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserCard entities. Represents the UserCards table.
  /// </summary>
  public DbSet<UserCard> UserCards { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for QuestDefinition entities. Represents the QuestDefinitions table.
  /// </summary>
  public DbSet<QuestDefinition> QuestDefinitions { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserQuest entities. Represents the UserQuests table.
  /// </summary>
  public DbSet<UserQuest> UserQuests { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserRanking entities. Represents the UserRankings table.
  /// </summary>
  public DbSet<UserRanking> UserRankings { get; set; } = null!;

  /// <summary>
  /// Gets or sets the DbSet for UserDailyReward entities. Represents the UserDailyRewards table.
  /// </summary>
  public DbSet<UserDailyReward> UserDailyRewards { get; set; } = null!;

  /// <summary>
  /// Configures the model that was discovered by convention from the entity types
  /// exposed in <see cref="DbSet{TEntity}"/> properties on your derived context.
  /// The resulting model may be cached and re-used for subsequent instances of your derived context.
  /// </summary>
  /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // 配置现有实体
    ConfigureCardDefinition(modelBuilder);
    ConfigureCardAbility(modelBuilder);

    // 配置用户管理实体
    ConfigureUserEntities(modelBuilder);

    // 配置经济系统实体
    ConfigureEconomyEntities(modelBuilder);
  }

  /// <summary>
  /// 配置CardDefinition实体
  /// </summary>
  private void ConfigureCardDefinition(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<CardDefinition>(entity =>
    {
      entity.ToTable("CardDefinitions");

      // 配置主键不使用自动递增，使用我们提供的 ID 值
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Id).ValueGeneratedNever();

      // 配置 AbilityIds 属性 - 将 List<long> 转换为 JSON 字符串存储
      entity.Property(e => e.AbilityIds)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<List<long>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<long>()
        );

      // 配置 Keywords 属性 - 将 List<string> 转换为 JSON 字符串存储
      entity.Property(e => e.Keywords)
        .HasConversion(
          v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)
        );

      // 配置 Tribe 属性 - 将 List<string> 转换为 JSON 字符串存储
      entity.Property(e => e.Tribe)
        .HasConversion(
          v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)
        );
    });
  }

  /// <summary>
  /// 配置用户管理相关实体
  /// </summary>
  private void ConfigureUserEntities(ModelBuilder modelBuilder)
  {
    // 配置User实体
    modelBuilder.Entity<User>(entity =>
    {
      entity.ToTable("Users");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.Username).IsUnique();
      entity.HasIndex(e => e.Email).IsUnique();

      entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
      entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
      entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
      entity.Property(e => e.AvatarUrl).HasMaxLength(500);
      entity.Property(e => e.Bio).HasMaxLength(500);
    });

    // 配置UserProfile实体
    modelBuilder.Entity<UserProfile>(entity =>
    {
      entity.ToTable("UserProfiles");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId).IsUnique();

      entity.Property(e => e.FavoriteCardId).HasMaxLength(50);
      entity.Property(e => e.WinRate).HasPrecision(5, 2);

      // 配置与User的一对一关系
      entity.HasOne(e => e.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });

    // 配置GameSession实体
    modelBuilder.Entity<GameSession>(entity =>
    {
      entity.ToTable("GameSessions");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.GameStateId);

      entity.Property(e => e.GameStateId).HasMaxLength(100).IsRequired();

      // 配置与User的多对一关系
      entity.HasOne(e => e.User)
                .WithMany(u => u.GameSessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });
  }

  /// <summary>
  /// 配置CardAbility实体
  /// </summary>
  private void ConfigureCardAbility(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<AbilityDefinition>(entity =>
    {
      entity.ToTable("CardAbilities");
      entity.HasKey(e => e.Id);

      // 配置索引
      entity.HasIndex(e => e.CardId);
      entity.HasIndex(e => e.AbilityTag);
      entity.HasIndex(e => new { e.CardId, e.IsActive });

      // 配置与CardDefinition的关系
      entity.HasOne(e => e.CardDefinition)
                .WithMany()
                .HasForeignKey(e => e.CardId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置字段约束
      entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

      entity.Property(e => e.Description)
                .HasMaxLength(500);

      entity.Property(e => e.AbilityTag)
                .IsRequired()
                .HasMaxLength(50);

      // 注意：移除了简单字符串表达式字段的配置
      // 统一使用DSL或JSON格式存储复杂配置

      // 配置时间戳字段
      entity.Property(e => e.CreatedAt)
                .IsRequired();

      entity.Property(e => e.UpdatedAt)
                .IsRequired();

      // 配置版本字段（乐观锁）
      entity.Property(e => e.Version)
                .IsRowVersion();
    });
  }

  /// <summary>
  /// 配置经济系统相关实体
  /// </summary>
  private void ConfigureEconomyEntities(ModelBuilder modelBuilder)
  {
    // 配置UserEconomy实体
    modelBuilder.Entity<UserEconomy>(entity =>
    {
      entity.ToTable("UserEconomies");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId).IsUnique();

      // 配置与User的一对一关系
      entity.HasOne(e => e.User)
                .WithOne(u => u.Economy)
                .HasForeignKey<UserEconomy>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置与UserRanking的一对一关系
      entity.HasOne(e => e.Ranking)
                .WithOne()
                .HasForeignKey<UserRanking>(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置与UserDailyReward的一对一关系
      entity.HasOne(e => e.DailyReward)
                .WithOne()
                .HasForeignKey<UserDailyReward>(dr => dr.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });

    // 配置CoinTransaction实体
    modelBuilder.Entity<CoinTransaction>(entity =>
    {
      entity.ToTable("CoinTransactions");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.CreatedAt);

      entity.Property(e => e.Description).HasMaxLength(500);
      entity.Property(e => e.RelatedEntityId).HasMaxLength(100);

      // 配置与User的多对一关系
      entity.HasOne(e => e.User)
                .WithMany(u => u.CoinTransactions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });

    // 配置CardPackDefinition实体
    modelBuilder.Entity<CardPackDefinition>(entity =>
    {
      entity.ToTable("CardPackDefinitions");
      entity.HasKey(e => e.Id);

      entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
      entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
    });

    // 配置UserCardPack实体
    modelBuilder.Entity<UserCardPack>(entity =>
    {
      entity.ToTable("UserCardPacks");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.CardPackDefinitionId);

      // 配置与User的多对一关系
      entity.HasOne(e => e.User)
                .WithMany(u => u.CardPacks)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置与CardPackDefinition的多对一关系
      entity.HasOne(e => e.CardPackDefinition)
                .WithMany(cpd => cpd.UserCardPacks)
                .HasForeignKey(e => e.CardPackDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
    });

    // 配置UserCard实体
    modelBuilder.Entity<UserCard>(entity =>
    {
      entity.ToTable("UserCards");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.CardDefinitionId);
      entity.HasIndex(e => e.CardPackId);

      // 配置与User的多对一关系
      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置与UserCardPack的多对一关系
      entity.HasOne(e => e.CardPack)
                .WithMany(ucp => ucp.Cards)
                .HasForeignKey(e => e.CardPackId)
                .OnDelete(DeleteBehavior.SetNull);

      // 配置与CardDefinition的多对一关系
      entity.HasOne(e => e.CardDefinition)
                .WithMany()
                .HasForeignKey(e => e.CardDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
    });

    // 配置QuestDefinition实体
    modelBuilder.Entity<QuestDefinition>(entity =>
    {
      entity.ToTable("QuestDefinitions");
      entity.HasKey(e => e.Id);

      entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
      entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
    });

    // 配置UserQuest实体
    modelBuilder.Entity<UserQuest>(entity =>
    {
      entity.ToTable("UserQuests");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId);
      entity.HasIndex(e => e.QuestDefinitionId);
      entity.HasIndex(e => e.ExpiresAt);

      // 配置与User的多对一关系 (通过UserEconomy)
      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

      // 配置与QuestDefinition的多对一关系
      entity.HasOne(e => e.QuestDefinition)
                .WithMany(qd => qd.UserQuests)
                .HasForeignKey(e => e.QuestDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
    });

    // 配置UserRanking实体
    modelBuilder.Entity<UserRanking>(entity =>
    {
      entity.ToTable("UserRankings");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId).IsUnique(); // 每个用户只有一个排名记录
      entity.HasIndex(e => e.Rank);
      entity.HasIndex(e => e.RankPoints);

      // 配置与User的一对一关系
      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });

    // 配置UserDailyReward实体
    modelBuilder.Entity<UserDailyReward>(entity =>
    {
      entity.ToTable("UserDailyRewards");
      entity.HasKey(e => e.Id);
      entity.HasIndex(e => e.UserId).IsUnique(); // 每个用户只有一个每日奖励记录
      entity.HasIndex(e => e.LastClaimedAt);
      entity.HasIndex(e => e.ConsecutiveDays);

      // 配置与User的一对一关系
      entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
    });
  }
}
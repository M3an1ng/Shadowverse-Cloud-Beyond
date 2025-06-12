//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 数据库设计: 文档/02-后端架构/02-数据库设计.md
//
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Data.SeedData
{
    /// <summary>
    /// 数据库种子数据服务，用于初始化测试数据
    /// </summary>
    public class DatabaseSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseSeeder> _logger;

        public DatabaseSeeder(ApplicationDbContext context, ILogger<DatabaseSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 执行数据库种子数据初始化
        /// </summary>
        public async Task SeedAsync()
        {
            try
            {
                _logger.LogInformation("Starting database seeding...");

                // 确保数据库已创建
                await _context.Database.EnsureCreatedAsync();

                // 检查是否已有CardAbility数据
                var existingAbilities = await _context.Set<Domain.Entities.AbilityDefinition>()
                    .CountAsync();

                if (existingAbilities > 0)
                {
                    _logger.LogInformation("CardAbility data already exists ({Count} records). Skipping seed.", existingAbilities);
                    return;
                }

                // 执行CardAbility种子数据
                await SeedCardAbilitiesAsync();

                _logger.LogInformation("Database seeding completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding.");
                throw;
            }
        }

        /// <summary>
        /// 种子CardAbility数据
        /// </summary>
        private async Task SeedCardAbilitiesAsync()
        {
            try
            {
                _logger.LogInformation("Seeding CardAbility data...");

                // 读取SQL脚本文件 - 修复路径构建逻辑
                // 首先尝试从源代码目录加载（开发环境）
                var sourceScriptPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Infrastructure", "Data", "SeedData", "CardAbilitySeedData.sql");

                // 如果源代码目录不存在，尝试从bin目录加载（发布环境）
                var binScriptPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Infrastructure", "Data", "SeedData", "CardAbilitySeedData.sql");

                var scriptPath = File.Exists(sourceScriptPath) ? sourceScriptPath : binScriptPath;

                if (!File.Exists(scriptPath))
                {
                    _logger.LogWarning("CardAbility seed script not found at: {ScriptPath}", scriptPath);
                    return;
                }

                var sqlScript = await File.ReadAllTextAsync(scriptPath);

                // 分割SQL语句（按分号分割）
                var statements = sqlScript.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var statement in statements)
                {
                    var trimmedStatement = statement.Trim();
                    if (string.IsNullOrEmpty(trimmedStatement) || trimmedStatement.StartsWith("--"))
                    {
                        continue; // 跳过空语句和注释
                    }

                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(trimmedStatement);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing SQL statement: {Statement}", trimmedStatement);
                        throw;
                    }
                }

                _logger.LogInformation("CardAbility seed data inserted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding CardAbility data.");
                throw;
            }
        }

        /// <summary>
        /// 清理种子数据（用于测试）
        /// </summary>
        public async Task CleanSeedDataAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning seed data...");

                // 删除所有CardAbility记录
                var abilities = _context.Set<Domain.Entities.AbilityDefinition>();
                _context.RemoveRange(abilities);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Seed data cleaned successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning seed data.");
                throw;
            }
        }

        /// <summary>
        /// 检查种子数据状态
        /// </summary>
        public async Task<bool> IsSeedDataPresentAsync()
        {
            try
            {
                var count = await _context.Set<Domain.Entities.AbilityDefinition>()
                    .CountAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking seed data status.");
                return false;
            }
        }
    }
}

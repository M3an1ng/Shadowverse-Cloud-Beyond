//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ShadowverseBeyond.Server.Infrastructure.Data.SeedData
{
    /// <summary>
    /// 服务集合扩展方法，用于注册数据库种子数据服务
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册数据库种子数据服务
        /// </summary>
        public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
        {
            services.AddScoped<DatabaseSeeder>();
            return services;
        }

        /// <summary>
        /// 在应用启动时执行数据库种子数据初始化
        /// </summary>
        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();
            
            try
            {
                logger.LogInformation("Initializing database seed data...");
                
                var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
                await seeder.SeedAsync();
                
                logger.LogInformation("Database seed data initialization completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize database seed data.");
                throw;
            }
        }

        /// <summary>
        /// 在开发环境中自动执行数据库种子数据初始化
        /// </summary>
        public static async Task SeedDatabaseInDevelopmentAsync(this IServiceProvider serviceProvider, IHostEnvironment environment)
        {
            if (environment.IsDevelopment())
            {
                await serviceProvider.SeedDatabaseAsync();
            }
        }
    }
}

//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using Microsoft.Extensions.DependencyInjection;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// DSL函数系统的依赖注入扩展方法
    /// </summary>
    public static class DslFunctionServiceExtensions
    {
        /// <summary>
        /// 注册DSL函数系统相关服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDslFunctionSystem(this IServiceCollection services)
        {
            // 注册函数注册表（单例）
            services.AddSingleton<IFunctionRegistry, FunctionRegistry>();

            // 注册DSL求值器
            // services.AddSingleton<DslEvaluator>(); // Obsolete, replaced by DslCompiler

            return services;
        }

        /// <summary>
        /// 注册自定义函数到函数注册表
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="customFunctions">自定义函数列表</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddCustomDslFunctions(
            this IServiceCollection services,
            params IFunctionDefinition[] customFunctions)
        {
            // 注册自定义函数到服务容器
            foreach (var function in customFunctions)
            {
                services.AddSingleton(function);
            }

            // 配置函数注册表以包含自定义函数
            services.Configure<FunctionRegistryOptions>(options =>
            {
                options.CustomFunctions.AddRange(customFunctions);
            });

            return services;
        }
    }

    /// <summary>
    /// 函数注册表配置选项
    /// </summary>
    public class FunctionRegistryOptions
    {
        public List<IFunctionDefinition> CustomFunctions { get; set; } = new();
    }
}

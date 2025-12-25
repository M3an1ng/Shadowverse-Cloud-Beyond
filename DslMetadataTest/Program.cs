using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions;
using ShadowverseBeyond.Server.Services;

namespace DslMetadataTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DSL元数据测试 ===\n");

            // 设置依赖注入
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IFunctionRegistry, FunctionRegistry>();
            services.AddSingleton<IDslMetadataService, ShadowverseBeyond.Server.Services.DslMetadataService>();

            var serviceProvider = services.BuildServiceProvider();

            // 获取DSL元数据服务
            var dslMetadataService = serviceProvider.GetRequiredService<IDslMetadataService>();

            try
            {
                // 获取元数据
                var metadata = dslMetadataService.GetDslMetadata();

                Console.WriteLine($"上下文对象数量: {metadata.ContextObjects.Count}");
                Console.WriteLine($"函数数量: {metadata.Functions.Count}\n");

                // 显示上下文对象
                Console.WriteLine("=== 上下文对象 ===");
                foreach (var contextObj in metadata.ContextObjects)
                {
                    Console.WriteLine($"对象: {contextObj.Name} ({contextObj.DisplayName})");
                    Console.WriteLine($"  描述: {contextObj.Description}");
                    Console.WriteLine($"  类型: {contextObj.TypeName}");
                    Console.WriteLine($"  属性数量: {contextObj.Properties.Count}");

                    foreach (var prop in contextObj.Properties)
                    {
                        Console.WriteLine($"    - {prop.Name} ({prop.DisplayName}): {prop.TypeName}");
                        if (!string.IsNullOrEmpty(prop.Description))
                        {
                            Console.WriteLine($"      描述: {prop.Description}");
                        }
                    }
                    Console.WriteLine();
                }

                // 显示函数
                Console.WriteLine("=== 函数 ===");
                foreach (var function in metadata.Functions)
                {
                    Console.WriteLine($"函数: {function.Name}");
                    Console.WriteLine($"  描述: {function.Description}");
                    Console.WriteLine($"  返回类型: {function.ReturnTypeName}");
                    Console.WriteLine($"  参数数量: {function.Parameters.Count}");

                    foreach (var param in function.Parameters)
                    {
                        var optional = param.IsOptional ? " (可选)" : "";
                        Console.WriteLine($"    - {param.Name}: {param.TypeName}{optional}");
                        if (!string.IsNullOrEmpty(param.Description))
                        {
                            Console.WriteLine($"      描述: {param.Description}");
                        }
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("测试完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}

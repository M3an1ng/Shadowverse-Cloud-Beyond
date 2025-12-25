using System;
using System.Linq;
using System.Reflection;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters;
using ShadowverseBeyond.Server.Domain.Abilities.Effects.Handlers.Parameters.Metadata;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== 效果参数类元数据测试 ===\n");

        // 获取所有继承自 BaseEffectParams 的类型
        var assembly = Assembly.GetAssembly(typeof(BaseEffectParams));
        if (assembly == null)
        {
            Console.WriteLine("无法获取程序集");
            return;
        }

        var effectParamTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BaseEffectParams)) && !t.IsAbstract)
            .OrderBy(t => t.Name);

        int totalTypes = 0;
        int annotatedTypes = 0;
        int totalProperties = 0;
        int annotatedProperties = 0;

        foreach (var type in effectParamTypes)
        {
            totalTypes++;
            Console.WriteLine($"类型: {type.Name}");

            // 检查类级别的 EffectMetadata 特性
            var effectMetadata = type.GetCustomAttribute<EffectMetadataAttribute>();
            if (effectMetadata != null)
            {
                annotatedTypes++;
                Console.WriteLine($"  ✓ 效果元数据: {effectMetadata.DisplayName} - {effectMetadata.Description}");
            }
            else
            {
                Console.WriteLine($"  ✗ 缺少 EffectMetadata 特性");
            }

            // 检查属性级别的 ParameterMetadata 特性
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType == type); // 只检查当前类声明的属性

            foreach (var property in properties)
            {
                totalProperties++;
                var paramMetadata = property.GetCustomAttribute<ParameterMetadataAttribute>();
                if (paramMetadata != null)
                {
                    annotatedProperties++;
                    Console.WriteLine($"    ✓ {property.Name}: {paramMetadata.DisplayName} ({paramMetadata.ControlType})");
                    if (!string.IsNullOrEmpty(paramMetadata.Description))
                    {
                        Console.WriteLine($"      描述: {paramMetadata.Description}");
                    }
                }
                else
                {
                    Console.WriteLine($"    ✗ {property.Name}: 缺少 ParameterMetadata 特性");
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("=== 统计结果 ===");
        Console.WriteLine($"总类型数: {totalTypes}");
        Console.WriteLine($"已标注类型: {annotatedTypes}");
        Console.WriteLine($"类型标注率: {(double)annotatedTypes / totalTypes * 100:F1}%");
        Console.WriteLine($"总属性数: {totalProperties}");
        Console.WriteLine($"已标注属性: {annotatedProperties}");
        Console.WriteLine($"属性标注率: {(double)annotatedProperties / totalProperties * 100:F1}%");
        Console.WriteLine("\n测试完成！");
    }
}

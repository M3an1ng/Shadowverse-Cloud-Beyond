using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    /// <summary>
    /// 值配置类，支持静态值、动态值、公式计算等多种值类型
    /// 为DSL语法解析提供基础支持
    /// </summary>
    public class ValueConfig
    {
        /// <summary>
        /// 值类型
        /// </summary>
        public ValueType Type { get; set; } = ValueType.Static;

        /// <summary>
        /// 静态值
        /// </summary>
        public object StaticValue { get; set; } = 0;

        /// <summary>
        /// 动态值源路径（如 "self.attack", "target.health"）
        /// </summary>
        public string DynamicSource { get; set; } = string.Empty;

        /// <summary>
        /// 动态表达式（测试兼容性属性，映射到DynamicSource）
        /// </summary>
        public string DynamicExpression
        {
            get => DynamicSource;
            set => DynamicSource = value;
        }

        /// <summary>
        /// 计算公式（支持数学表达式）
        /// </summary>
        public string Formula { get; set; } = string.Empty;

        /// <summary>
        /// 变量名（从能力变量系统获取）
        /// </summary>
        public string VariableName { get; set; } = string.Empty;

        /// <summary>
        /// 随机值配置
        /// </summary>
        public RandomConfig? RandomConfig { get; set; }

        /// <summary>
        /// 最小值限制
        /// </summary>
        public int MinValue { get; set; } = int.MinValue;

        /// <summary>
        /// 最大值限制
        /// </summary>
        public int MaxValue { get; set; } = int.MaxValue;

        /// <summary>
        /// 额外参数（用于复杂值计算）
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// 创建静态值配置
        /// </summary>
        /// <param name="value">静态值</param>
        /// <returns>值配置</returns>
        public static ValueConfig Static(object value)
        {
            return new ValueConfig
            {
                Type = ValueType.Static,
                StaticValue = value
            };
        }

        /// <summary>
        /// 创建动态值配置
        /// </summary>
        /// <param name="source">动态值源</param>
        /// <returns>值配置</returns>
        public static ValueConfig Dynamic(string source)
        {
            return new ValueConfig
            {
                Type = ValueType.Dynamic,
                DynamicSource = source
            };
        }

        /// <summary>
        /// 创建公式值配置
        /// </summary>
        /// <param name="formula">计算公式</param>
        /// <returns>值配置</returns>
        public static ValueConfig CreateFormulaValue(string formula)
        {
            return new ValueConfig
            {
                Type = ValueType.Formula,
                Formula = formula
            };
        }

        /// <summary>
        /// 创建变量值配置
        /// </summary>
        /// <param name="variableName">变量名</param>
        /// <returns>值配置</returns>
        public static ValueConfig Variable(string variableName)
        {
            return new ValueConfig
            {
                Type = ValueType.Variable,
                VariableName = variableName
            };
        }

        /// <summary>
        /// 创建随机值配置
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns>值配置</returns>
        public static ValueConfig Random(int min, int max)
        {
            return new ValueConfig
            {
                Type = ValueType.Random,
                RandomConfig = new RandomConfig { MinValue = min, MaxValue = max }
            };
        }

        /// <summary>
        /// Asynchronously resolves the configured value based on the context.
        /// </summary>
        /// <param name="context">The query or ability context.</param>
        /// <param name="card">The specific card instance, if relevant for dynamic resolution.</param>
        /// <returns>The resolved value, or null if resolution fails.</returns>
        public async Task<object?> ResolveAsync(Events.GameContext context, Entities.CardInstance? card = null)
        {
            // TODO: Implement the actual resolution logic based on context
            // This requires access to game state, variable system, expression evaluator etc.
            object? resolvedValue = Type switch
            {
                ValueType.Static => StaticValue,
                ValueType.Dynamic => await ResolveDynamicValueAsync(context, card), // Placeholder
                ValueType.Formula => await ResolveFormulaValueAsync(context, card), // Placeholder
                ValueType.Variable => await ResolveVariableValueAsync(context), // Placeholder
                ValueType.Random => ResolveRandomValue(), // Placeholder
                _ => null
            };

            // Apply clamping if the resolved value is numeric
            if (resolvedValue != null && TryConvertToDecimal(resolvedValue, out var decimalValue))
            {
                if (decimalValue < MinValue) return MinValue;
                if (decimalValue > MaxValue) return MaxValue;
                // Return the clamped decimal or potentially convert back to original type if needed?
                // For simplicity, returning the potentially clamped decimal.
                return decimalValue;
            }

            return resolvedValue;
        }

        // Placeholder methods for complex resolution types
        private Task<object?> ResolveDynamicValueAsync(Events.GameContext context, Entities.CardInstance? card)
        {
            // TODO: Implement logic to parse DynamicSource and fetch value from context/card
            Console.WriteLine($"Warning: Dynamic value resolution for '{DynamicSource}' not implemented.");
            return Task.FromResult<object?>(null); // Or throw NotImplementedException
        }

        private Task<object?> ResolveFormulaValueAsync(Events.GameContext context, Entities.CardInstance? card)
        {
            // TODO: Implement logic to parse and evaluate Formula using an expression evaluator
            Console.WriteLine($"Warning: Formula value resolution for '{Formula}' not implemented.");
            return Task.FromResult<object?>(null); // Or throw NotImplementedException
        }

        private Task<object?> ResolveVariableValueAsync(Events.GameContext context)
        {
            // TODO: Implement logic to fetch value from AbilityVariables system via context
            Console.WriteLine($"Warning: Variable value resolution for '{VariableName}' not implemented.");
            return Task.FromResult<object?>(null); // Or throw NotImplementedException
        }

        private object? ResolveRandomValue()
        {
            if (RandomConfig == null) return null;
            // TODO: Implement proper random generation, potentially using context for seed
            var random = RandomConfig.Seed.HasValue ? new Random(RandomConfig.Seed.Value) : new Random();
            return random.Next(RandomConfig.MinValue, RandomConfig.MaxValue + 1); // +1 because Random.Next max is exclusive
        }

        private bool TryConvertToDecimal(object? value, out decimal result)
        {
            result = 0;
            if (value == null) return false;
            try
            {
                result = Convert.ToDecimal(value);
                return true;
            }
            catch { return false; } // Catch all conversion exceptions for simplicity
        }
    }

    /// <summary>
    /// 随机值配置
    /// </summary>
    public class RandomConfig
    {
        /// <summary>
        /// 最小值
        /// </summary>
        public int MinValue { get; set; } = 0;

        /// <summary>
        /// 最大值
        /// </summary>
        public int MaxValue { get; set; } = 1;

        /// <summary>
        /// 随机种子（可选）
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// 权重列表（用于加权随机）
        /// </summary>
        public List<int>? Weights { get; set; }

        /// <summary>
        /// 候选值列表（用于从列表中随机选择）
        /// </summary>
        public List<object>? Candidates { get; set; }
    }

    /// <summary>
    /// 过滤器配置
    /// </summary>
    public class FilterConfig
    {
        /// <summary>
        /// 过滤属性路径（如 "attack", "health", "keywords"）
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equals;

        /// <summary>
        /// The source of the value to check on the target entity (e.g., "attack", "keywords").
        /// </summary>
        public ValueConfig ValueSource { get; set; } = new(); // Renamed from Property to ValueSource for clarity

        /// <summary>
        /// The value to compare against.
        /// </summary>
        public ValueConfig TargetValue { get; set; } = new(); // Replaced 'Value' with 'TargetValue'

        /// <summary>
        /// 逻辑操作符（与下一个过滤器的关系）
        /// </summary>
        public LogicOperator Logic { get; set; } = LogicOperator.And;

        /// <summary>
        /// 是否取反
        /// </summary>
        public bool Negate { get; set; } = false;

        /// <summary>
        /// 创建简单过滤器
        /// </summary>
        /// <param name="property">属性</param>
        /// <param name="op">操作符</param>
        /// <param name="sourceProperty">源属性路径</param>
        /// <param name="op">操作符</param>
        /// <param name="targetStaticValue">目标静态值</param>
        /// <returns>过滤器配置</returns>
        public static FilterConfig Create(string sourceProperty, ComparisonOperator op, object targetStaticValue)
        {
            return new FilterConfig
            {
                ValueSource = ValueConfig.Dynamic(sourceProperty), // Assume property path is dynamic source
                Operator = op,
                TargetValue = ValueConfig.Static(targetStaticValue) // Target is static value
            };
        }
    }
}

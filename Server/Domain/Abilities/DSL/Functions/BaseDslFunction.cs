//
// 相关文档:
// - 卡牌能力系统: 文档/05-游戏系统/01-卡牌能力系统.md
// - 服务层设计: 文档/02-后端架构/01-服务层设计.md
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ShadowverseBeyond.Server.Domain.Abilities.DSL.Evaluation;

using ShadowverseBeyond.Server.Domain.Events;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Functions
{
    /// <summary>
    /// DSL函数定义接口
    /// </summary>
    public interface IFunctionDefinition
    {
        /// <summary>
        /// 函数名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The C# type that this function is expected to return.
        /// This is used by the DslCompiler to generate strongly-typed expression trees.
        /// The default is 'object' to support functions that don't have a specific, fixed return type.
        /// </summary>
        Type ReturnType { get; }

        /// <summary>
        /// A user-friendly description of the function for UI tooltips.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// A structured list of metadata for each parameter the function accepts.
        /// </summary>
        IReadOnlyList<DslParameterInfo> Parameters { get; }

        /// <summary>
        /// 验证函数参数
        /// </summary>
        /// <param name="args">参数列表</param>
        /// <returns>如果参数有效返回true，否则返回false</returns>
        bool ValidateArguments(List<object?> args);

        /// <summary>
        /// 执行函数
        /// </summary>
        /// <param name="args">参数列表</param>
        /// <param name="context">评估上下文</param>
        /// <returns>函数执行结果</returns>
        object? Execute(List<object?> args, GameContext context);
    }

    /// <summary>
    /// DSL函数的基础抽象类，提供通用的参数验证和类型转换功能
    /// </summary>
    public abstract class BaseDslFunction : IFunctionDefinition
    {
        public abstract string Name { get; }

        public virtual Type ReturnType => typeof(object);

        public virtual string Description => string.Empty;

        public virtual IReadOnlyList<DslParameterInfo> Parameters => Array.Empty<DslParameterInfo>();

        /// <summary>
        /// 验证函数参数
        /// </summary>
        public virtual bool ValidateArguments(List<object?> args)
        {
            var expectedCount = GetExpectedArgumentCount();
            if (expectedCount.HasValue && args.Count != expectedCount.Value)
            {
                return false;
            }

            var minCount = GetMinimumArgumentCount();
            if (args.Count < minCount)
            {
                return false;
            }

            var maxCount = GetMaximumArgumentCount();
            if (maxCount.HasValue && args.Count > maxCount.Value)
            {
                return false;
            }

            return ValidateArgumentTypes(args);
        }

        /// <summary>
        /// 执行函数
        /// </summary>
        public abstract object? Execute(List<object?> args, GameContext context);

        /// <summary>
        /// 获取期望的参数数量（如果固定）
        /// </summary>
        protected virtual int? GetExpectedArgumentCount() => null;

        /// <summary>
        /// 获取最小参数数量
        /// </summary>
        protected virtual int GetMinimumArgumentCount() => 0;

        /// <summary>
        /// 获取最大参数数量
        /// </summary>
        protected virtual int? GetMaximumArgumentCount() => null;

        /// <summary>
        /// 验证参数类型
        /// </summary>
        protected virtual bool ValidateArgumentTypes(List<object?> args) => true;

        /// <summary>
        /// 将对象转换为整数
        /// </summary>
        protected static int ConvertToInt(object? value, int defaultValue = 0)
        {
            return value switch
            {
                int intValue => intValue,
                double doubleValue => (int)doubleValue,
                float floatValue => (int)floatValue,
                decimal decimalValue => (int)decimalValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => defaultValue
            };
        }

        /// <summary>
        /// 将对象转换为双精度浮点数
        /// </summary>
        protected static double ConvertToDouble(object? value, double defaultValue = 0.0)
        {
            return value switch
            {
                double doubleValue => doubleValue,
                int intValue => intValue,
                float floatValue => floatValue,
                decimal decimalValue => (double)decimalValue,
                string stringValue when double.TryParse(stringValue, out var parsed) => parsed,
                _ => defaultValue
            };
        }

        /// <summary>
        /// 将对象转换为布尔值
        /// </summary>
        protected static bool ConvertToBool(object? value, bool defaultValue = false)
        {
            return value switch
            {
                bool boolValue => boolValue,
                int intValue => intValue != 0,
                double doubleValue => doubleValue != 0.0,
                string stringValue => !string.IsNullOrEmpty(stringValue) &&
                                     !stringValue.Equals("false", StringComparison.OrdinalIgnoreCase),
                null => false,
                _ => defaultValue
            };
        }

        /// <summary>
        /// 将对象转换为字符串
        /// </summary>
        protected static string ConvertToString(object? value, string defaultValue = "")
        {
            return value?.ToString() ?? defaultValue;
        }

        /// <summary>
        /// 检查对象是否为集合类型
        /// </summary>
        protected static bool IsCollection(object? value)
        {
            return value is IEnumerable && !(value is string);
        }

        /// <summary>
        /// 将对象转换为可枚举集合
        /// </summary>
        protected static IEnumerable<object?> ConvertToEnumerable(object? value)
        {
            return value switch
            {
                IEnumerable enumerable when !(value is string) =>
                    enumerable.Cast<object?>(),
                null => Enumerable.Empty<object?>(),
                _ => new[] { value }
            };
        }

        /// <summary>
        /// 从对象中获取指定属性的值
        /// </summary>
        protected static object? GetPropertyValue(object? obj, string propertyName)
        {
            if (obj == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var type = obj.GetType();
            var property = type.GetProperty(propertyName,
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.IgnoreCase);

            return property?.GetValue(obj);
        }

        /// <summary>
        /// 验证参数数量是否在指定范围内
        /// </summary>
        protected static bool ValidateArgumentCount(List<object?> args, int min, int? max = null)
        {
            if (args.Count < min)
            {
                return false;
            }

            if (max.HasValue && args.Count > max.Value)
            {
                return false;
            }

            return true;
        }
    }
}

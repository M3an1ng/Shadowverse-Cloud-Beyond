using System;

namespace ShadowverseBeyond.Server.Domain.Abilities.Common
{
    /// <summary>
    /// 类型转换工具类
    /// 提供统一的类型转换逻辑，避免重复代码
    /// </summary>
    public static class TypeConverter
    {
        /// <summary>
        /// 将对象转换为布尔值
        /// 统一的转换规则：
        /// - null -> false
        /// - bool -> 直接返回
        /// - string -> 空字符串或"false"为false，其他为true
        /// - 数值类型 -> 0为false，非0为true
        /// - 其他类型 -> 非null为true
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <returns>转换后的布尔值</returns>
        public static bool ConvertToBool(object? value)
        {
            if (value == null)
                return false;

            if (value is bool boolValue)
                return boolValue;

            if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue) && 
                       !string.Equals(stringValue, "false", StringComparison.OrdinalIgnoreCase);
            }

            if (value is int intValue)
                return intValue != 0;

            if (value is double doubleValue)
                return Math.Abs(doubleValue) > double.Epsilon;

            if (value is decimal decimalValue)
                return decimalValue != 0;

            if (value is float floatValue)
                return Math.Abs(floatValue) > float.Epsilon;

            if (value is long longValue)
                return longValue != 0;

            if (value is short shortValue)
                return shortValue != 0;

            if (value is byte byteValue)
                return byteValue != 0;

            // 对于其他类型，非null即为true
            return true;
        }

        /// <summary>
        /// 将对象转换为整数
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="defaultValue">转换失败时的默认值</param>
        /// <returns>转换后的整数值</returns>
        public static int ConvertToInt(object? value, int defaultValue = 0)
        {
            if (value == null)
                return defaultValue;

            if (value is int intValue)
                return intValue;

            if (value is double doubleValue)
                return (int)Math.Round(doubleValue);

            if (value is decimal decimalValue)
                return (int)Math.Round(decimalValue);

            if (value is float floatValue)
                return (int)Math.Round(floatValue);

            if (value is long longValue)
                return (int)Math.Min(Math.Max(longValue, int.MinValue), int.MaxValue);

            if (value is string stringValue && int.TryParse(stringValue, out var parsedInt))
                return parsedInt;

            if (value is bool boolValue)
                return boolValue ? 1 : 0;

            return defaultValue;
        }

        /// <summary>
        /// 将对象转换为字符串
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="defaultValue">转换失败时的默认值</param>
        /// <returns>转换后的字符串值</returns>
        public static string ConvertToString(object? value, string defaultValue = "")
        {
            if (value == null)
                return defaultValue;

            if (value is string stringValue)
                return stringValue;

            return value.ToString() ?? defaultValue;
        }

        /// <summary>
        /// 将对象转换为双精度浮点数
        /// </summary>
        /// <param name="value">要转换的值</param>
        /// <param name="defaultValue">转换失败时的默认值</param>
        /// <returns>转换后的双精度浮点数值</returns>
        public static double ConvertToDouble(object? value, double defaultValue = 0.0)
        {
            if (value == null)
                return defaultValue;

            if (value is double doubleValue)
                return doubleValue;

            if (value is int intValue)
                return intValue;

            if (value is decimal decimalValue)
                return (double)decimalValue;

            if (value is float floatValue)
                return floatValue;

            if (value is long longValue)
                return longValue;

            if (value is string stringValue && double.TryParse(stringValue, out var parsedDouble))
                return parsedDouble;

            if (value is bool boolValue)
                return boolValue ? 1.0 : 0.0;

            return defaultValue;
        }

        /// <summary>
        /// 检查值是否为数值零
        /// </summary>
        /// <param name="value">要检查的值</param>
        /// <returns>如果是数值零则返回true</returns>
        public static bool IsZero(object? value)
        {
            if (value == null)
                return false;

            if (value is int intValue)
                return intValue == 0;

            if (value is double doubleValue)
                return Math.Abs(doubleValue) < double.Epsilon;

            if (value is decimal decimalValue)
                return decimalValue == 0;

            if (value is float floatValue)
                return Math.Abs(floatValue) < float.Epsilon;

            if (value is long longValue)
                return longValue == 0;

            if (value is short shortValue)
                return shortValue == 0;

            if (value is byte byteValue)
                return byteValue == 0;

            return false;
        }
    }
}

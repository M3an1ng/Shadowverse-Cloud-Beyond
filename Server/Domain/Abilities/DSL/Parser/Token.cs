using System;
using System.Collections.Generic;

namespace ShadowverseBeyond.Server.Domain.Abilities.DSL.Parser
{
    /// <summary>
    /// 词法单元（Token）
    /// 表示DSL源代码中的一个词法单元
    /// </summary>
    public class Token
    {
        /// <summary>
        /// 词法单元类型
        /// </summary>
        public TokenType Type { get; set; }

        /// <summary>
        /// 词法单元的文本值
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 在源代码中的位置
        /// </summary>
        public TokenPosition Position { get; set; } = new();

        /// <summary>
        /// 词法单元的数据类型（用于字面量）
        /// </summary>
        public Type? DataType { get; set; }

        /// <summary>
        /// 解析后的值（用于字面量）
        /// </summary>
        public object? ParsedValue { get; set; }

        public Token(TokenType type, string value, TokenPosition position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        public override string ToString()
        {
            return $"{Type}({Value}) at {Position}";
        }
    }

    /// <summary>
    /// 词法单元类型枚举
    /// </summary>
    public enum TokenType
    {
        // 基础类型
        EOF,            // 文件结束
        Newline,        // 换行符
        Whitespace,     // 空白字符
        Comment,        // 注释

        // 字面量
        Number,         // 数字
        String,         // 字符串
        Boolean,        // 布尔值
        Null,           // 空值

        // 标识符和关键字
        Identifier,     // 标识符
        Keyword,        // 关键字

        // 操作符
        Plus,           // +
        Minus,          // -
        Multiply,       // *
        Divide,         // /
        Modulo,         // %
        Power,          // **

        // 比较操作符
        Equal,          // ==
        NotEqual,       // !=
        GreaterThan,    // >
        LessThan,       // <
        GreaterEqual,   // >=
        LessEqual,      // <=

        // 逻辑操作符
        And,            // and, &&
        Or,             // or, ||
        Not,            // not, !

        // 集合操作符
        SetUnion,       // |
        SetIntersect,   // &

        // 赋值操作符
        Assign,         // =
        PlusAssign,     // +=
        MinusAssign,    // -=

        // 分隔符
        LeftParen,      // (
        RightParen,     // )
        LeftBracket,    // [
        RightBracket,   // ]
        LeftBrace,      // {
        RightBrace,     // }
        Comma,          // ,
        Semicolon,      // ;
        Colon,          // :
        Dot,            // .
        Arrow,          // ->
        Question,       // ?

        // 特殊标记
        Invalid,        // 无效的词法单元
        Unknown         // 未知类型
    }

    /// <summary>
    /// 词法单元位置信息
    /// </summary>
    public class TokenPosition
    {
        /// <summary>
        /// 行号（从1开始）
        /// </summary>
        public int Line { get; set; } = 1;

        /// <summary>
        /// 列号（从1开始）
        /// </summary>
        public int Column { get; set; } = 1;

        /// <summary>
        /// 在源代码中的绝对位置
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// 词法单元的长度
        /// </summary>
        public int Length { get; set; } = 0;

        public TokenPosition(int line = 1, int column = 1, int index = 0, int length = 0)
        {
            Line = line;
            Column = column;
            Index = index;
            Length = length;
        }

        public override string ToString()
        {
            return $"Line {Line}, Column {Column}";
        }
    }

    /// <summary>
    /// 关键字映射表
    /// </summary>
    public static class Keywords
    {
        private static readonly Dictionary<string, TokenType> _keywords = new()
        {
            // 基础关键字
            ["true"] = TokenType.Boolean,
            ["false"] = TokenType.Boolean,
            ["null"] = TokenType.Null,
            ["and"] = TokenType.And,
            ["or"] = TokenType.Or,
            ["not"] = TokenType.Not,
        };

        /// <summary>
        /// 检查是否为关键字
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>关键字类型，如果不是关键字则返回null</returns>
        public static TokenType? GetKeywordType(string word)
        {
            return _keywords.TryGetValue(word.ToLowerInvariant(), out var type) ? type : null;
        }

        /// <summary>
        /// 获取所有关键字
        /// </summary>
        /// <returns>关键字列表</returns>
        public static IEnumerable<string> GetAllKeywords()
        {
            return _keywords.Keys;
        }
    }

    /// <summary>
    /// 操作符映射表
    /// </summary>
    public static class Operators
    {
        private static readonly Dictionary<string, TokenType> _operators = new()
        {
            // 算术操作符
            ["+"] = TokenType.Plus,
            ["-"] = TokenType.Minus,
            ["*"] = TokenType.Multiply,
            ["/"] = TokenType.Divide,
            ["%"] = TokenType.Modulo,
            ["**"] = TokenType.Power,

            // 比较操作符
            ["=="] = TokenType.Equal,
            ["!="] = TokenType.NotEqual,
            [">"] = TokenType.GreaterThan,
            ["<"] = TokenType.LessThan,
            [">="] = TokenType.GreaterEqual,
            ["<="] = TokenType.LessEqual,

            // 逻辑操作符
            ["&&"] = TokenType.And,
            ["||"] = TokenType.Or,
            ["!"] = TokenType.Not,

            // 集合操作符
            ["|"] = TokenType.SetUnion,
            ["&"] = TokenType.SetIntersect,

            // 赋值操作符
            ["="] = TokenType.Assign,
            ["+="] = TokenType.PlusAssign,
            ["-="] = TokenType.MinusAssign,

            // 其他操作符
            ["->"] = TokenType.Arrow,
            ["?"] = TokenType.Question
        };

        /// <summary>
        /// 获取操作符类型
        /// </summary>
        /// <param name="op">操作符字符串</param>
        /// <returns>操作符类型，如果不是操作符则返回null</returns>
        public static TokenType? GetOperatorType(string op)
        {
            return _operators.TryGetValue(op, out var type) ? type : null;
        }

        /// <summary>
        /// 获取所有操作符
        /// </summary>
        /// <returns>操作符列表</returns>
        public static IEnumerable<string> GetAllOperators()
        {
            return _operators.Keys;
        }

        /// <summary>
        /// 检查字符是否可能是操作符的开始
        /// </summary>
        /// <param name="c">字符</param>
        /// <returns>是否可能是操作符开始</returns>
        public static bool IsOperatorStart(char c)
        {
            return c switch
            {
                '+' or '-' or '*' or '/' or '%' or '=' or '!' or '>' or '<' or '&' or '|' or '?' => true,
                _ => false
            };
        }
    }
}
